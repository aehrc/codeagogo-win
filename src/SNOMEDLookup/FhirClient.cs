using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace SNOMEDLookup;

/// <summary>
/// FHIR terminology client for SNOMED CT lookups with multi-edition support.
/// </summary>
public sealed class FhirClient
{
    private readonly HttpClient _http;
    private readonly LruCache<string, ConceptResult> _cache;
    private string _baseUrl;

    // International Edition module ID - tried first
    private const string InternationalModuleId = "900000000000207008";
    private static readonly string InternationalSystem = $"http://snomed.info/sct/{InternationalModuleId}";

    // Retry configuration
    private const int MaxRetries = 2; // 3 total attempts
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.Zero, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public FhirClient(string? baseUrl = null, HttpClient? http = null)
    {
        _baseUrl = (baseUrl ?? "https://tx.ontoserver.csiro.au/fhir").TrimEnd('/');
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        _cache = new LruCache<string, ConceptResult>(100, TimeSpan.FromHours(6));
    }

    /// <summary>
    /// Updates the FHIR base URL.
    /// </summary>
    public void SetBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        Log.Info($"FHIR base URL set to: {_baseUrl}");
    }

    /// <summary>
    /// Main entry point: looks up a SNOMED CT concept by ID.
    /// Searches International edition first, then all available editions if not found.
    /// </summary>
    public async Task<ConceptResult> LookupAsync(string conceptId, CancellationToken ct = default)
    {
        // Check cache first
        if (_cache.TryGet(conceptId, out var cached) && cached != null)
        {
            Log.Debug($"Cache hit for concept {conceptId}");
            return cached;
        }

        Log.Info($"Looking up concept {conceptId}");

        // Try International Edition first
        try
        {
            var result = await LookupInSystemAsync(conceptId, InternationalSystem, null, ct);
            if (result != null)
            {
                _cache.Set(conceptId, result);
                return result;
            }
        }
        catch (ConceptNotFoundException)
        {
            Log.Debug($"Concept {conceptId} not found in International edition");
        }

        // Fetch all editions and search in parallel
        var editions = await FetchAllEditionsAsync(ct);
        Log.Info($"Searching {editions.Count} editions for concept {conceptId}");

        var result2 = await LookupInAllEditionsAsync(conceptId, editions, ct);
        if (result2 != null)
        {
            _cache.Set(conceptId, result2);
            return result2;
        }

        throw new ConceptNotFoundException($"Concept {conceptId} not found in any SNOMED CT edition");
    }

    /// <summary>
    /// Looks up a concept in a specific SNOMED CT system/edition.
    /// </summary>
    private async Task<ConceptResult?> LookupInSystemAsync(string conceptId, string system, string? version, CancellationToken ct)
    {
        var url = $"{_baseUrl}/CodeSystem/$lookup?system={Uri.EscapeDataString(system)}&code={Uri.EscapeDataString(conceptId)}";
        if (!string.IsNullOrEmpty(version))
        {
            url += $"&version={Uri.EscapeDataString(version)}";
        }

        Log.Debug($"FHIR lookup: {url}");

        try
        {
            var parameters = await GetJsonWithRetryAsync<FhirParameters>(url, ct);
            return ParseLookupResponse(conceptId, system, parameters);
        }
        catch (ApiException ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConceptNotFoundException($"Concept {conceptId} not found");
        }
    }

    /// <summary>
    /// Parses the FHIR Parameters response from a $lookup operation.
    /// </summary>
    private static ConceptResult? ParseLookupResponse(string conceptId, string system, FhirParameters parameters)
    {
        string? fsn = null;
        string? pt = null;
        bool? active = null;
        string? moduleId = null;
        string? version = null;

        foreach (var param in parameters.Parameter ?? Array.Empty<FhirParameter>())
        {
            switch (param.Name)
            {
                case "name":
                    fsn = param.ValueString;
                    break;
                case "display":
                    pt = param.ValueString;
                    break;
                case "inactive":
                    active = !param.ValueBoolean;
                    break;
                case "version":
                    version = param.ValueString;
                    break;
                case "property":
                    var propertyCode = param.Part?.FirstOrDefault(p => p.Name == "code")?.ValueCode;
                    var propertyValue = param.Part?.FirstOrDefault(p => p.Name == "value");
                    if (propertyCode == "moduleId" && propertyValue?.ValueCode != null)
                    {
                        moduleId = propertyValue.ValueCode;
                    }
                    break;
                case "designation":
                    var use = param.Part?.FirstOrDefault(p => p.Name == "use")?.ValueCoding;
                    var designationValue = param.Part?.FirstOrDefault(p => p.Name == "value")?.ValueString;
                    if (use?.Code == "900000000000003001" && designationValue != null) // FSN
                    {
                        fsn = designationValue;
                    }
                    else if (use?.Code == "900000000000013009" && designationValue != null) // Synonym (PT)
                    {
                        pt ??= designationValue;
                    }
                    break;
            }
        }

        // If we didn't get an FSN from designation, use display
        if (string.IsNullOrEmpty(fsn) && !string.IsNullOrEmpty(pt))
        {
            fsn = pt;
        }

        // Extract module from system URL if not in properties
        if (string.IsNullOrEmpty(moduleId) && system.StartsWith("http://snomed.info/sct/"))
        {
            moduleId = system["http://snomed.info/sct/".Length..].Split('/')[0];
        }

        var edition = EditionNames.GetEditionName(moduleId);

        return new ConceptResult(
            ConceptId: conceptId,
            Branch: system,
            Fsn: fsn,
            Pt: pt ?? fsn,
            Active: active ?? true,
            EffectiveTime: version,
            ModuleId: moduleId,
            Edition: edition
        );
    }

    /// <summary>
    /// Fetches all available SNOMED CT editions from the FHIR server.
    /// </summary>
    private async Task<List<SnomedEdition>> FetchAllEditionsAsync(CancellationToken ct)
    {
        var url = $"{_baseUrl}/CodeSystem?url=http://snomed.info/sct&_elements=url,version,title,name";
        Log.Debug($"Fetching editions: {url}");

        try
        {
            var bundle = await GetJsonWithRetryAsync<FhirBundle>(url, ct);
            var editions = new List<SnomedEdition>();

            foreach (var entry in bundle.Entry ?? Array.Empty<FhirEntry>())
            {
                var cs = entry.Resource;
                if (cs?.Url == null) continue;

                editions.Add(new SnomedEdition
                {
                    System = cs.Url,
                    Version = cs.Version,
                    Title = cs.Title ?? cs.Name ?? "Unknown"
                });
            }

            // Ensure International edition is first if present
            var intl = editions.FirstOrDefault(e => e.System.Contains(InternationalModuleId));
            if (intl != null)
            {
                editions.Remove(intl);
                editions.Insert(0, intl);
            }

            Log.Info($"Found {editions.Count} SNOMED CT editions");
            return editions;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch editions: {ex.Message}");
            // Return at least International edition
            return new List<SnomedEdition>
            {
                new() { System = InternationalSystem, Title = "International" }
            };
        }
    }

    /// <summary>
    /// Searches for a concept across all editions in parallel, returning first match.
    /// </summary>
    private async Task<ConceptResult?> LookupInAllEditionsAsync(string conceptId, List<SnomedEdition> editions, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = new List<Task<ConceptResult?>>();

        foreach (var edition in editions)
        {
            tasks.Add(LookupInEditionWithCancelAsync(conceptId, edition, cts.Token));
        }

        // Wait for first successful result or all to complete
        while (tasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(tasks);
            tasks.Remove(completedTask);

            try
            {
                var result = await completedTask;
                if (result != null)
                {
                    // Cancel remaining tasks
                    await cts.CancelAsync();
                    Log.Debug($"Found concept {conceptId} in edition: {result.Edition}");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when we cancel after finding result
            }
            catch (Exception ex)
            {
                Log.Debug($"Edition search failed: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Wraps LookupInSystemAsync with cancellation support.
    /// </summary>
    private async Task<ConceptResult?> LookupInEditionWithCancelAsync(string conceptId, SnomedEdition edition, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            return await LookupInSystemAsync(conceptId, edition.System, edition.Version, ct);
        }
        catch (ConceptNotFoundException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Debug($"Lookup in {edition.Title} failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Makes an HTTP GET request with retry logic and exponential backoff.
    /// </summary>
    private async Task<T> GetJsonWithRetryAsync<T>(string url, CancellationToken ct)
    {
        Exception? lastException = null;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delay = RetryDelays[Math.Min(attempt, RetryDelays.Length - 1)];
                Log.Debug($"Retry attempt {attempt} after {delay.TotalMilliseconds}ms");
                await Task.Delay(delay, ct);
            }

            try
            {
                using var response = await _http.GetAsync(url, ct);
                var body = await response.Content.ReadAsStringAsync(ct);

                Log.Debug($"HTTP {(int)response.StatusCode} {Log.Snippet(url, 80)}");

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<T>(body, JsonOpts)
                        ?? throw new ApiException("Empty response from FHIR server");
                }

                // Don't retry client errors (4xx)
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new ConceptNotFoundException("Resource not found");
                    }
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        throw new RateLimitException("Rate limit exceeded");
                    }
                    throw new ApiException($"Client error ({(int)response.StatusCode})");
                }

                // Server error (5xx) - will retry
                lastException = new ApiException($"Server error ({(int)response.StatusCode})");
            }
            catch (HttpRequestException ex)
            {
                Log.Debug($"Connection error: {ex.Message}");
                lastException = new ApiException("Network error. Check your internet connection.", ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                Log.Debug($"Request timeout: {ex.Message}");
                lastException = new ApiException("Request timed out", ex);
            }
            catch (ApiException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        throw lastException ?? new ApiException("Request failed after retries");
    }
}

#region FHIR DTOs

public sealed class FhirParameters
{
    public string? ResourceType { get; set; }
    public FhirParameter[]? Parameter { get; set; }
}

public sealed class FhirParameter
{
    public string? Name { get; set; }
    public string? ValueString { get; set; }
    public string? ValueCode { get; set; }
    public bool? ValueBoolean { get; set; }
    public FhirCoding? ValueCoding { get; set; }
    public FhirPart[]? Part { get; set; }
}

public sealed class FhirPart
{
    public string? Name { get; set; }
    public string? ValueString { get; set; }
    public string? ValueCode { get; set; }
    public FhirCoding? ValueCoding { get; set; }
}

public sealed class FhirCoding
{
    public string? System { get; set; }
    public string? Code { get; set; }
    public string? Display { get; set; }
}

public sealed class FhirBundle
{
    public string? ResourceType { get; set; }
    public FhirEntry[]? Entry { get; set; }
}

public sealed class FhirEntry
{
    public FhirCodeSystem? Resource { get; set; }
}

public sealed class FhirCodeSystem
{
    public string? ResourceType { get; set; }
    public string? Url { get; set; }
    public string? Version { get; set; }
    public string? Name { get; set; }
    public string? Title { get; set; }
}

public sealed class SnomedEdition
{
    public string System { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Title { get; set; } = string.Empty;
}

#endregion
