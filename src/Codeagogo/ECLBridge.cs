// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Reflection;
using System.Text.Json;
using Jint;

namespace Codeagogo;

/// <summary>
/// Bridge to ecl-core (TypeScript) running in Jint (pure .NET JavaScript interpreter).
/// Provides ECL parsing, formatting, validation, and concept extraction powered by the
/// ecl-core library bundled as an embedded JavaScript resource.
/// </summary>
/// <remarks>
/// Jint Engine is not thread-safe. All calls should happen on the same thread.
/// For background use, create a separate instance per thread.
/// </remarks>
public sealed class ECLBridge
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Engine _engine;
    private bool _loaded;

    /// <summary>
    /// Creates a new ECL bridge, loading the ecl-core bundle from the embedded resource.
    /// </summary>
    public ECLBridge()
    {
        _engine = new Engine();
        LoadBundle();
    }

    /// <summary>
    /// Creates a bridge with a pre-loaded JS string (useful for testing).
    /// </summary>
    public ECLBridge(string bundleSource)
    {
        _engine = new Engine();
        try
        {
            _engine.Execute(bundleSource);
            _loaded = true;
        }
        catch (Exception ex)
        {
            Log.Error($"ECLBridge: failed to execute bundle: {ex.Message}");
        }
    }

    private void LoadBundle()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Codeagogo.ecl-core-bundle.js");
            if (stream == null)
            {
                Log.Error("ECLBridge: could not find embedded ecl-core-bundle.js");
                return;
            }

            using var reader = new StreamReader(stream);
            var js = reader.ReadToEnd();
            _engine.Execute(js);
            _loaded = true;
            Log.Info("ECLBridge: loaded ecl-core bundle");
        }
        catch (Exception ex)
        {
            Log.Error($"ECLBridge: failed to load bundle: {ex.Message}");
        }
    }

    /// <summary>
    /// Whether the ecl-core bundle loaded successfully.
    /// </summary>
    public bool IsLoaded => _loaded;

    // ── Formatting ──────────────────────────────────────────────────

    /// <summary>
    /// Formats an ECL expression using ecl-core's formatter.
    /// </summary>
    /// <param name="ecl">The ECL expression to format</param>
    /// <param name="options">Formatting options (defaults to ecl-core defaults)</param>
    /// <returns>The formatted ECL string, or null if formatting failed</returns>
    public string? FormatECL(string ecl, FormattingOptions? options = null)
    {
        if (!_loaded) return null;
        var opts = options ?? FormattingOptions.Default;
        var escaped = EscapeForJS(ecl);

        try
        {
            var result = _engine.Evaluate($@"
                (function() {{
                    try {{
                        return ECLCore.formatDocument('{escaped}', {opts.ToJsObject()});
                    }} catch(e) {{
                        return null;
                    }}
                }})()
            ");

            if (result.IsNull() || result.IsUndefined()) return null;
            return result.AsString();
        }
        catch (Exception ex)
        {
            Log.Debug($"ECLBridge.FormatECL error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Toggles an ECL expression between formatted and minified forms.
    /// </summary>
    /// <param name="ecl">The ECL expression to toggle</param>
    /// <returns>The toggled ECL string, or null if the input is not valid ECL</returns>
    public string? ToggleECLFormat(string ecl)
    {
        if (!_loaded) return null;
        var escaped = EscapeForJS(ecl);

        try
        {
            var result = _engine.Evaluate($@"
                (function() {{
                    try {{
                        var opts = ECLCore.defaultFormattingOptions;
                        var formatted = ECLCore.formatDocument('{escaped}', opts);
                        var input = '{escaped}'.trim().replace(/\r\n/g, '\n');
                        if (input === formatted.trim()) {{
                            return ECLCore.formatDocument('{escaped}', {{
                                indentSize: 0,
                                indentStyle: 'space',
                                spaceAroundOperators: true,
                                maxLineLength: 0,
                                alignTerms: false,
                                wrapComments: false,
                                breakOnOperators: false,
                                breakOnRefinementComma: false,
                                breakAfterColon: false,
                            }});
                        }}
                        return formatted;
                    }} catch(e) {{
                        return null;
                    }}
                }})()
            ");

            if (result.IsNull() || result.IsUndefined()) return null;
            return result.AsString();
        }
        catch (Exception ex)
        {
            Log.Debug($"ECLBridge.ToggleECLFormat error: {ex.Message}");
            return null;
        }
    }

    // ── Parsing ─────────────────────────────────────────────────────

    /// <summary>
    /// Parses an ECL expression and returns structured results.
    /// </summary>
    /// <param name="ecl">The ECL expression to parse</param>
    /// <returns>A ParseResult with AST presence, errors, and warnings</returns>
    public ParseResult ParseECL(string ecl)
    {
        if (!_loaded) return new ParseResult(false, [], []);
        var escaped = EscapeForJS(ecl);

        try
        {
            var result = _engine.Evaluate($@"
                (function() {{
                    var r = ECLCore.parseECL('{escaped}');
                    return JSON.stringify({{
                        hasAST: r.ast !== null,
                        errors: r.errors.map(function(e) {{
                            return {{ line: e.line, column: e.column, message: e.message }};
                        }}),
                        warnings: r.warnings.map(function(w) {{ return w.message || String(w); }})
                    }});
                }})()
            ");

            var json = result.AsString();
            var parsed = JsonSerializer.Deserialize<ParseResultJson>(json, JsonOpts);
            if (parsed == null) return new ParseResult(false, [], []);

            var errors = parsed.Errors?.Select(e =>
                new ParseError(e.Line, e.Column, e.Message ?? "Unknown error")).ToList() ?? [];

            return new ParseResult(parsed.HasAST, errors, parsed.Warnings ?? []);
        }
        catch (Exception ex)
        {
            Log.Debug($"ECLBridge.ParseECL error: {ex.Message}");
            return new ParseResult(false, [], []);
        }
    }

    // ── Validation ──────────────────────────────────────────────────

    /// <summary>
    /// Checks whether a string is a valid SNOMED CT concept ID (Verhoeff check via ecl-core).
    /// </summary>
    public bool IsValidConceptId(string sctid)
    {
        if (!_loaded) return false;
        try
        {
            var result = _engine.Evaluate($"ECLCore.isValidConceptId('{sctid}')");
            return result.AsBoolean();
        }
        catch { return false; }
    }

    /// <summary>
    /// Checks whether an ECL expression is syntactically valid.
    /// </summary>
    public bool IsValidECL(string ecl)
    {
        var result = ParseECL(ecl);
        return result.HasAST && result.Errors.Count == 0;
    }

    // ── Concept Extraction ──────────────────────────────────────────

    /// <summary>
    /// Extracts concept IDs referenced in an ECL expression.
    /// </summary>
    /// <param name="ecl">The ECL expression to analyze</param>
    /// <returns>Array of concept references with IDs and optional display terms</returns>
    public List<ConceptReference> ExtractConceptIds(string ecl)
    {
        if (!_loaded) return [];
        var escaped = EscapeForJS(ecl);

        try
        {
            var result = _engine.Evaluate($@"
                (function() {{
                    var r = ECLCore.parseECL('{escaped}');
                    if (!r.ast) return '[]';
                    var concepts = ECLCore.extractConceptIds(r.ast);
                    return JSON.stringify(concepts.map(function(c) {{
                        return {{ id: c.id, term: c.term || null }};
                    }}));
                }})()
            ");

            var json = result.AsString();
            return JsonSerializer.Deserialize<List<ConceptReference>>(json, JsonOpts) ?? [];
        }
        catch (Exception ex)
        {
            Log.Debug($"ECLBridge.ExtractConceptIds error: {ex.Message}");
            return [];
        }
    }

    // ── Knowledge Base ──────────────────────────────────────────────

    /// <summary>
    /// Returns all ECL knowledge articles from ecl-core's knowledge base.
    /// </summary>
    public List<KnowledgeArticle> GetArticles()
    {
        if (!_loaded) return [];

        try
        {
            var result = _engine.Evaluate(@"
                (function() {
                    var articles = ECLCore.allArticles;
                    if (!articles || !Array.isArray(articles)) return '[]';
                    return JSON.stringify(articles.map(function(a) {
                        return {
                            id: a.id || '',
                            category: a.category || '',
                            name: a.name || '',
                            summary: a.summary || '',
                            content: a.content || '',
                            examples: a.examples || []
                        };
                    }));
                })()
            ");

            var json = result.AsString();
            return JsonSerializer.Deserialize<List<KnowledgeArticle>>(json, JsonOpts) ?? [];
        }
        catch (Exception ex)
        {
            Log.Debug($"ECLBridge.GetArticles error: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Returns ECL operator reference documentation from ecl-core's knowledge base.
    /// </summary>
    public List<OperatorDoc> GetOperatorDocs()
    {
        if (!_loaded) return [];

        try
        {
            var result = _engine.Evaluate(@"
                (function() {
                    var docs = ECLCore.operatorHoverDocs;
                    if (!docs || !Array.isArray(docs)) return '[]';
                    return JSON.stringify(docs.map(function(d) {
                        return { symbol: d.operator || '', markdown: d.markdown || '' };
                    }));
                })()
            ");

            var json = result.AsString();
            return JsonSerializer.Deserialize<List<OperatorDoc>>(json, JsonOpts) ?? [];
        }
        catch (Exception ex)
        {
            Log.Debug($"ECLBridge.GetOperatorDocs error: {ex.Message}");
            return [];
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Escapes a string for safe inclusion in a JS single-quoted string literal.
    /// </summary>
    private static string EscapeForJS(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    // ── Inner Types ─────────────────────────────────────────────────

    /// <summary>
    /// Formatting options matching ecl-core's FormattingOptions.
    /// </summary>
    public sealed class FormattingOptions
    {
        public int IndentSize { get; set; } = 2;
        public string IndentStyle { get; set; } = "space";
        public bool SpaceAroundOperators { get; set; } = true;
        public int MaxLineLength { get; set; } = 80;
        public bool AlignTerms { get; set; } = true;
        public bool WrapComments { get; set; }
        public bool BreakOnOperators { get; set; }
        public bool BreakOnRefinementComma { get; set; }
        public bool BreakAfterColon { get; set; }

        public static FormattingOptions Default { get; } = new();

        internal string ToJsObject() => $$"""
            {
                indentSize: {{IndentSize}},
                indentStyle: '{{IndentStyle}}',
                spaceAroundOperators: {{SpaceAroundOperators.ToString().ToLowerInvariant()}},
                maxLineLength: {{MaxLineLength}},
                alignTerms: {{AlignTerms.ToString().ToLowerInvariant()}},
                wrapComments: {{WrapComments.ToString().ToLowerInvariant()}},
                breakOnOperators: {{BreakOnOperators.ToString().ToLowerInvariant()}},
                breakOnRefinementComma: {{BreakOnRefinementComma.ToString().ToLowerInvariant()}},
                breakAfterColon: {{BreakAfterColon.ToString().ToLowerInvariant()}}
            }
            """;
    }

    /// <summary>
    /// A parse error with location information.
    /// </summary>
    public sealed record ParseError(int Line, int Column, string Message);

    /// <summary>
    /// A concept reference extracted from an ECL expression.
    /// </summary>
    public sealed record ConceptReference(string Id, string? Term);

    /// <summary>
    /// An ECL operator reference documentation entry.
    /// </summary>
    public sealed record OperatorDoc(string Symbol, string Markdown);

    /// <summary>
    /// An ECL knowledge article from ecl-core's knowledge base.
    /// </summary>
    public sealed record KnowledgeArticle(
        string Id, string Category, string Name,
        string Summary, string Content, List<string> Examples);

    /// <summary>
    /// Result of parsing an ECL expression.
    /// </summary>
    public sealed record ParseResult(bool HasAST, List<ParseError> Errors, List<string> Warnings);

    // JSON deserialization helpers
    private sealed record ParseResultJson(bool HasAST, List<ParseErrorJson>? Errors, List<string>? Warnings);
    private sealed record ParseErrorJson(int Line, int Column, string? Message);
}
