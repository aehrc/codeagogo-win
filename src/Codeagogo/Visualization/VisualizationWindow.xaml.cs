// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace Codeagogo.Visualization;

public partial class VisualizationWindow : Window
{
    private readonly OntoserverClient _client;
    private readonly string _conceptId;
    private readonly string? _preferredTerm;
    private string? _svgContent;
    private string? _htmlContent;
    private double _zoomLevel = 1.0;

    public VisualizationWindow(OntoserverClient client, string conceptId, string? preferredTerm = null)
    {
        InitializeComponent();
        _client = client;
        _conceptId = conceptId;
        _preferredTerm = preferredTerm;

        TitleText.Text = $"Concept Diagram: {conceptId}";
        SubtitleText.Text = preferredTerm ?? "";

        Loaded += async (_, _) => await LoadDiagramAsync();
    }

    private async Task LoadDiagramAsync()
    {
        try
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            ErrorOverlay.Visibility = Visibility.Collapsed;

            // Run all network-heavy work on a background thread to keep the UI responsive.
            // Only touch UI elements via Dispatcher when updating status text.
            LoadingStatusText.Text = "Fetching concept properties...";

            var result = await Task.Run(async () =>
            {
                // Step 1: Fetch concept properties
                var properties = await _client.LookupWithPropertiesAsync(_conceptId).ConfigureAwait(false);
                if (properties == null) return ((ConceptVisualizationData, Dictionary<string, bool>, string, string, bool)?)null;

                // Step 2: Parse normal form for defining relationships
                ConceptVisualizationData vizData;
                try
                {
                    vizData = BuildVisualizationData(properties);
                }
                catch (Exception ex)
                {
                    Log.Error($"diagram: BuildVisualizationData failed: {ex.Message}");
                    vizData = new ConceptVisualizationData
                    {
                        ConceptId = properties.ConceptId,
                        PreferredTerm = properties.PreferredTerm ?? _preferredTerm,
                        FullySpecifiedName = properties.FullySpecifiedName,
                        SufficientlyDefined = properties.SufficientlyDefined
                    };
                    foreach (var parent in properties.ParentCodes)
                        vizData.Parents.Add(new ConceptReference(parent.Code, parent.Display));
                }

                // Step 2b: Replace decomposed nested expression values with pre-coordinated concepts
                // The normal form decomposes e.g. 258798001 "mg/mL" into (415777001:{num=mg},{denom=mL})
                // but the FHIR properties have the actual concept ID (258798001).
                // Use the property values so the server lookup returns the correct PT.
                vizData = ReplaceWithPreCoordinatedValues(vizData, properties.RelationshipProperties);

                // Step 3: Collect all concept IDs from normal form for batch lookup
                var allConceptIds = new HashSet<string>();
                foreach (var p in vizData.Parents)
                    allConceptIds.Add(p.ConceptId);
                foreach (var a in vizData.UngroupedAttributes)
                {
                    allConceptIds.Add(a.Type.ConceptId);
                    allConceptIds.Add(a.Value.ConceptId);
                }
                foreach (var g in vizData.AttributeGroups)
                    foreach (var a in g.Attributes)
                    {
                        allConceptIds.Add(a.Type.ConceptId);
                        allConceptIds.Add(a.Value.ConceptId);
                    }
                // Note: we intentionally do NOT scan the raw normalForm string for IDs here.
                // The vizData attributes already have the correct IDs (including pre-coordinated
                // replacements from step 2b). Scanning the raw normalForm would re-add the
                // decomposed IDs (e.g., 415777001) that we just replaced with 258798001.
                allConceptIds.Remove(_conceptId);
                allConceptIds.Remove("concrete");
                allConceptIds.Remove("nested");

                // Step 4: Batch lookup for terms and definition status
                var definitionStatusMap = new Dictionary<string, bool>
                {
                    [_conceptId] = properties.SufficientlyDefined
                };

                if (allConceptIds.Count > 0)
                {
                    // Two parallel passes:
                    // 1. Definition status via LookupWithPropertiesAsync (tries International first)
                    // 2. Display names via no-version lookup (returns server default edition PT,
                    //    e.g., "Ampoule" from SCTAU instead of "Ampule" from International)
                    var defTasks = allConceptIds.Select(async id =>
                    {
                        try
                        {
                            var propResult = await _client.LookupWithPropertiesAsync(id).ConfigureAwait(false);
                            return (id, defined: propResult?.SufficientlyDefined);
                        }
                        catch { return (id, defined: (bool?)null); }
                    });

                    var ptTasks = allConceptIds.Select(async id =>
                    {
                        try
                        {
                            // No version = server returns default edition preferred term
                            // (e.g., "Ampoule" from SCTAU, not "Ampule" from International)
                            var result = await _client.LookupDefaultEditionAsync(id).ConfigureAwait(false);
                            return (id, pt: result?.Pt);
                        }
                        catch { return (id, pt: (string?)null); }
                    });

                    var defResults = await Task.WhenAll(defTasks).ConfigureAwait(false);
                    var ptResults = await Task.WhenAll(ptTasks).ConfigureAwait(false);

                    foreach (var (id, defined) in defResults)
                    {
                        if (defined.HasValue)
                            definitionStatusMap[id] = defined.Value;
                    }

                    var displayNames = new BatchLookupResult(new(), new(), new());
                    foreach (var (id, pt) in ptResults)
                    {
                        if (!string.IsNullOrEmpty(pt))
                            displayNames.PtByCode[id] = pt;
                    }

                    vizData = EnrichWithTerms(vizData, displayNames);
                }

                // Step 5: Render diagram
                var svgContent = DiagramRenderer.RenderSvg(vizData, definitionStatusMap);
                var htmlContent = DiagramRenderer.RenderHtml(vizData, definitionStatusMap);

                return ((ConceptVisualizationData, Dictionary<string, bool>, string, string, bool)?)(vizData, definitionStatusMap, svgContent, htmlContent, properties.SufficientlyDefined);
            }).ConfigureAwait(true); // Resume on UI thread for WebView2

            if (result == null)
            {
                ShowError("Could not fetch concept properties from the server.");
                return;
            }

            // Unpack result (back on UI thread)
            var (vizData2, defMap, svg, html, suffDef) = result.Value;
            _svgContent = svg;
            _htmlContent = html;

            // Step 6: Display in WebView2 (must be on UI thread)
            await InitializeWebViewAndDisplay();

            var parentCount = vizData2.Parents.Count;
            var attrCount = vizData2.UngroupedAttributes.Count +
                           vizData2.AttributeGroups.Sum(g => g.Attributes.Count);
            StatusText.Text = $"{parentCount} parent(s), {attrCount} defining relationship(s)" +
                (suffDef ? " [Fully Defined]" : " [Primitive]");

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Log.Error($"Visualization error: {ex.Message}");
            ShowError($"Failed to generate diagram: {ex.Message}");
        }
    }

    private ConceptVisualizationData BuildVisualizationData(ConceptProperties properties)
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = properties.ConceptId,
            PreferredTerm = properties.PreferredTerm ?? _preferredTerm,
            FullySpecifiedName = properties.FullySpecifiedName,
            SufficientlyDefined = properties.SufficientlyDefined
        };

        // Parents always come from the FHIR property response
        foreach (var parent in properties.ParentCodes)
            data.Parents.Add(new ConceptReference(parent.Code, parent.Display));

        // Parse normal form for defining relationships (attributes and groups)
        if (!string.IsNullOrEmpty(properties.NormalForm))
        {
            var normalFormResult = NormalFormParser.Parse(properties.NormalForm);
            data.UngroupedAttributes.AddRange(normalFormResult.UngroupedAttributes);
            data.AttributeGroups.AddRange(normalFormResult.Groups);
        }

        return data;
    }

    private ConceptVisualizationData EnrichWithTerms(ConceptVisualizationData data, BatchLookupResult batch)
    {
        var enrichedParents = data.Parents.Select(p =>
        {
            if (batch.PtByCode.TryGetValue(p.ConceptId, out var pt) && !string.IsNullOrEmpty(pt))
                return new ConceptReference(p.ConceptId, pt);
            return string.IsNullOrEmpty(p.Term)
                ? new ConceptReference(p.ConceptId, p.ConceptId)
                : p;
        }).ToList();

        // Prefer server's preferred term when available (gives edition-appropriate terms
        // like "mg" instead of "milligram", "mg/mL" instead of "Unit of mass concentration").
        // Skip concrete values (numeric/string literals).
        ConceptReference EnrichRef(ConceptReference r)
        {
            if (r.ConceptId == "concrete")
                return r;

            if (batch.PtByCode.TryGetValue(r.ConceptId, out var pt) && !string.IsNullOrEmpty(pt))
                return new ConceptReference(r.ConceptId, pt);
            // Keep embedded term from normal form if lookup didn't find it
            return string.IsNullOrEmpty(r.Term)
                ? new ConceptReference(r.ConceptId, r.ConceptId)
                : r;
        }

        ConceptAttribute EnrichAttr(ConceptAttribute attr)
        {
            return new ConceptAttribute(EnrichRef(attr.Type), EnrichRef(attr.Value));
        }

        var enrichedUngrouped = data.UngroupedAttributes.Select(EnrichAttr).ToList();
        var enrichedGroups = data.AttributeGroups.Select(g =>
        {
            var eg = new AttributeGroup { GroupNumber = g.GroupNumber };
            eg.Attributes.AddRange(g.Attributes.Select(EnrichAttr));
            return eg;
        }).ToList();

        var enriched = new ConceptVisualizationData
        {
            ConceptId = data.ConceptId,
            PreferredTerm = data.PreferredTerm,
            FullySpecifiedName = data.FullySpecifiedName,
            SufficientlyDefined = data.SufficientlyDefined
        };
        enriched.Parents.AddRange(enrichedParents);
        enriched.UngroupedAttributes.AddRange(enrichedUngrouped);
        enriched.AttributeGroups.AddRange(enrichedGroups);
        return enriched;
    }

    /// <summary>
    /// Replaces decomposed nested expression values with their pre-coordinated concept IDs.
    /// E.g., the normal form decomposes 258798001 "mg/mL" into (415777001:{num,denom}),
    /// but the FHIR properties have the actual concept ID (258798001).
    /// </summary>
    public static ConceptVisualizationData ReplaceWithPreCoordinatedValues(
        ConceptVisualizationData vizData, Dictionary<string, string> relationshipProperties)
    {
        if (relationshipProperties.Count == 0) return vizData;

        ConceptAttribute FixAttr(ConceptAttribute attr)
        {
            var typeId = attr.Type.ConceptId;
            if (relationshipProperties.TryGetValue(typeId, out var preCoordId)
                && preCoordId != attr.Value.ConceptId
                && attr.Value.ConceptId != "concrete")
            {
                return new ConceptAttribute(attr.Type, new ConceptReference(preCoordId, null));
            }
            return attr;
        }

        vizData.UngroupedAttributes = vizData.UngroupedAttributes.Select(FixAttr).ToList();
        foreach (var g in vizData.AttributeGroups)
            g.Attributes = g.Attributes.Select(FixAttr).ToList();

        return vizData;
    }

    private async Task InitializeWebViewAndDisplay()
    {
        if (_htmlContent == null) return;

        try
        {
            await WebView.EnsureCoreWebView2Async();

            // Security hardening: disable features not needed for local SVG rendering
            WebView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            WebView.CoreWebView2.Settings.IsWebMessageEnabled = false;
            WebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            WebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            WebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;

            // Block navigation to external URLs; only allow about: and data: URIs
            WebView.CoreWebView2.NavigationStarting += (s, args) =>
            {
                if (!args.Uri.StartsWith("about:") && !args.Uri.StartsWith("data:"))
                {
                    args.Cancel = true;
                    Log.Debug($"Blocked external navigation to: {args.Uri}");
                }
            };

            WebView.NavigateToString(_htmlContent);
        }
        catch (Exception ex)
        {
            Log.Error($"WebView2 init error: {ex.Message}");

            // Fallback: open the diagram in the default browser
            Log.Info($"diagram: attempting browser fallback, htmlContent={(_htmlContent != null ? $"{_htmlContent.Length} chars" : "null")}");
            var htmlToOpen = _htmlContent;
            if (htmlToOpen != null)
            {
                try
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "Codeagogo");
                    Directory.CreateDirectory(tempDir);
                    var tempPath = Path.Combine(tempDir, $"diagram-{_conceptId}.html");
                    File.WriteAllText(tempPath, htmlToOpen);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = tempPath,
                        UseShellExecute = true
                    });
                    Log.Info($"WebView2 unavailable — opened diagram in browser: {tempPath}");
                    Close();
                    return;
                }
                catch (Exception fallbackEx)
                {
                    Log.Error($"Browser fallback failed: {fallbackEx.Message}");
                }
            }

            ShowError("WebView2 Runtime is not installed.\n\nConcept diagram visualization requires the Microsoft Edge WebView2 Runtime.\n\nDownload it from:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/");
        }
    }

    private void ShowError(string message)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        ErrorOverlay.Visibility = Visibility.Visible;
        ErrorText.Text = message;
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Min(3.0, _zoomLevel + 0.2);
        UpdateZoom();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = Math.Max(0.5, _zoomLevel - 0.2);
        UpdateZoom();
    }

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _zoomLevel = 1.0;
        UpdateZoom();
    }

    private void UpdateZoom()
    {
        ZoomLabel.Content = $"{(int)(_zoomLevel * 100)}%";
        if (WebView.CoreWebView2 != null)
        {
            WebView.CoreWebView2.ExecuteScriptAsync(
                $"document.getElementById('diagram').style.transform = 'scale({_zoomLevel:F2})'; void(0);");
        }
    }

    /// <summary>
    /// Builds a filesystem-safe filename from the concept ID and preferred term.
    /// </summary>
    private string BuildExportFileName(string extension)
    {
        var baseName = _conceptId;
        if (!string.IsNullOrWhiteSpace(_preferredTerm))
        {
            var sanitized = _preferredTerm
                .ToLowerInvariant()
                .Replace(' ', '-');
            sanitized = Regex.Replace(sanitized, @"[^a-z0-9\-]", "");
            sanitized = Regex.Replace(sanitized, @"-{2,}", "-");
            sanitized = sanitized.Trim('-');
            if (sanitized.Length > 60)
                sanitized = sanitized[..60].TrimEnd('-');
            if (!string.IsNullOrEmpty(sanitized))
                baseName = $"{_conceptId}-{sanitized}";
        }
        return $"{baseName}.{extension}";
    }

    private void SaveSvg_Click(object sender, RoutedEventArgs e)
    {
        if (_svgContent == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SVG Files (*.svg)|*.svg",
            DefaultExt = ".svg",
            FileName = BuildExportFileName("svg")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, _svgContent);
                StatusText.Text = $"Saved to {dialog.FileName}";
                Log.Info($"SVG saved to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save SVG: {ex.Message}");
                StatusText.Text = $"Save failed: {ex.Message}";
            }
        }
    }

    private async void SavePng_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2 == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PNG Files (*.png)|*.png",
            DefaultExt = ".png",
            FileName = BuildExportFileName("png")
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                using var stream = new FileStream(dialog.FileName, FileMode.Create);
                await WebView.CoreWebView2.CapturePreviewAsync(
                    Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
                StatusText.Text = $"Saved to {dialog.FileName}";
                Log.Info($"PNG saved to {dialog.FileName}");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to save PNG: {ex.Message}");
                StatusText.Text = $"Save failed: {ex.Message}";
            }
        }
    }

    private async void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2 == null) return;

        try
        {
            using var ms = new MemoryStream();
            await WebView.CoreWebView2.CapturePreviewAsync(
                Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, ms);
            ms.Position = 0;

            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();

            System.Windows.Clipboard.SetImage(image);
            StatusText.Text = "Copied to clipboard";
            Log.Info("Diagram copied to clipboard");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to copy diagram: {ex.Message}");
            StatusText.Text = $"Copy failed: {ex.Message}";
        }
    }
}
