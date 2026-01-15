// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Codeagogo;

/// <summary>
/// Client for looking up SNOMED CT concepts via a FHIR R4 terminology server.
/// Uses the CodeSystem/$lookup operation for concept retrieval.
/// </summary>
public sealed class OntoserverClient
{
    private static readonly Uri DefaultBaseUri = new("https://tx.ontoserver.csiro.au/fhir/");

    /// <summary>
    /// SNOMED CT International Edition module ID.
    /// </summary>
    private const string InternationalEditionId = "900000000000207008";

    /// <summary>
    /// SNOMED CT FSN (Fully Specified Name) designation type code.
    /// </summary>
    private const string FsnDesignationCode = "900000000000003001";

    private readonly HttpClient _http;
    private Uri _baseUrl;

    private readonly ConcurrentDictionary<string, (ConceptResult Result, DateTimeOffset Ts)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(6);

    /// <summary>
    /// Cached SNOMED editions with a 30-minute TTL to avoid re-fetching on every batch lookup.
    /// </summary>
    private static List<EditionInfo>? s_cachedEditions;
    private static DateTimeOffset s_editionsCacheTime;
    private static readonly TimeSpan EditionsCacheTtl = TimeSpan.FromMinutes(30);

    public OntoserverClient(HttpClient? http = null, string? baseUrl = null, string? installId = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/fhir+json"));
        _http.Timeout = TimeSpan.FromSeconds(30);

        // Set User-Agent with app version and anonymous install ID for usage metrics
        var version = typeof(OntoserverClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        var idSuffix = string.IsNullOrEmpty(installId) ? "" : $"; {installId}";
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Codeagogo/{version} (Windows{idSuffix})");

        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUri : new Uri(baseUrl);
        ValidateUrlScheme(_baseUrl);
        Log.Info($"OntoserverClient init base={_baseUrl}");
    }

    /// <summary>
    /// Updates the FHIR base URL.
    /// </summary>
    public void SetBaseUrl(string baseUrl)
    {
        var uri = new Uri(baseUrl.TrimEnd('/') + "/");
        ValidateUrlScheme(uri);
        _baseUrl = uri;
        Log.Info($"FHIR base URL set to: {_baseUrl}");
    }

    /// <summary>
    /// Validates that the URI uses http or https scheme.
    /// </summary>
    private static void ValidateUrlScheme(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("FHIR base URL must use http or https scheme");
        }

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            Log.Info("Warning: FHIR endpoint uses insecure HTTP");
        }
    }

    /// <summary>
    /// Executes an HTTP GET with exponential backoff retry.
    /// </summary>
    private async Task<HttpResponseMessage> GetWithRetryAsync(Uri url, int maxRetries = 2)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var resp = await _http.GetAsync(url);
                return resp;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = TimeSpan.FromMilliseconds(500 * Math.Pow(2, attempt));
                Log.Debug($"Retry {attempt + 1}/{maxRetries} after {delay.TotalMilliseconds}ms: {ex.Message}");
                await Task.Delay(delay);
            }
        }
        // This won't be reached but compiler needs it
        return await _http.GetAsync(url);
    }

    /// <summary>
    /// Looks up a code in a specific code system.
    /// </summary>
    /// <param name="code">The code to look up</param>
    /// <param name="system">The code system URI (e.g., "http://loinc.org")</param>
    /// <returns>The concept details</returns>
    public async Task<ConceptResult> LookupInCodeSystemAsync(string code, string system)
    {
        var cacheKey = $"{system}|{code}";
        if (_cache.TryGetValue(cacheKey, out var hit) && DateTimeOffset.UtcNow - hit.Ts < _ttl)
        {
            Log.Debug($"cache hit system={system} code={code}");
            return hit.Result;
        }

        Log.Info($"lookup code={code} system={system}");

        var url = BuildLookupUrlForSystem(code, system);
        Log.Debug($"GET {url}");

        try
        {
            using var resp = await GetWithRetryAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"Code {code} not found in {system}");
            }

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP {(int)resp.StatusCode} from server");
            }

            var parameters = JsonSerializer.Deserialize<FhirParameters>(body, JsonOpts);
            var result = ParseConceptFromParametersGeneric(parameters, code, system);

            if (result != null)
            {
                _cache[cacheKey] = (result, DateTimeOffset.UtcNow);
                return result;
            }

            throw new Exception($"Code {code} not found in {system}");
        }
        catch (Exception ex)
        {
            Log.Error($"lookup error system={system} code={code}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Looks up a code in multiple configured code systems, trying each until found.
    /// </summary>
    /// <param name="code">The code to look up</param>
    /// <param name="systems">The code system URIs to try</param>
    /// <returns>The concept details, or null if not found in any system</returns>
    public async Task<ConceptResult?> LookupInConfiguredSystemsAsync(string code, IEnumerable<string> systems)
    {
        foreach (var system in systems)
        {
            try
            {
                var result = await LookupInCodeSystemAsync(code, system);
                Log.Info($"found code={code} in system={system}");
                return result;
            }
            catch
            {
                // Try next system
                continue;
            }
        }

        Log.Info($"code={code} not found in any configured system");
        return null;
    }

    /// <summary>
    /// Gets available code systems from the server.
    /// </summary>
    /// <returns>List of available code systems</returns>
    public async Task<List<AvailableCodeSystem>> GetAvailableCodeSystemsAsync()
    {
        var url = new Uri(_baseUrl, "CodeSystem?_summary=true&_format=json");
        Log.Debug($"GET {url}");

        try
        {
            using var resp = await GetWithRetryAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Log.Error($"failed to fetch code systems: HTTP {(int)resp.StatusCode}");
                return new List<AvailableCodeSystem>();
            }

            var bundle = JsonSerializer.Deserialize<FhirBundle>(body, JsonOpts);
            var systems = new List<AvailableCodeSystem>();

            foreach (var entry in bundle?.Entry ?? Array.Empty<FhirBundleEntry>())
            {
                var cs = entry.Resource;
                if (cs?.Url == null) continue;

                systems.Add(new AvailableCodeSystem(
                    cs.Url,
                    cs.Title ?? cs.Name ?? cs.Url,
                    cs.Version
                ));
            }

            Log.Info($"found {systems.Count} code systems");
            return systems;
        }
        catch (Exception ex)
        {
            Log.Error($"failed to fetch code systems: {ex.Message}");
            return new List<AvailableCodeSystem>();
        }
    }

    /// <summary>
    /// Looks up a SNOMED CT concept by its identifier.
    /// </summary>
    /// <param name="conceptId">The SNOMED CT concept ID (6-18 digits)</param>
    /// <returns>The concept details including FSN, PT, and status</returns>
    public async Task<ConceptResult> LookupAsync(string conceptId)
    {
        if (_cache.TryGetValue(conceptId, out var hit) && DateTimeOffset.UtcNow - hit.Ts < _ttl)
        {
            Log.Debug($"cache hit conceptId={conceptId}");
            return hit.Result;
        }

        Log.Info($"lookup conceptId={conceptId}");

        // Step 1: Try International Edition first (most concepts are there)
        var result = await TryLookupInEditionAsync(conceptId, InternationalEditionId);
        if (result != null)
        {
            _cache[conceptId] = (result, DateTimeOffset.UtcNow);
            Log.Info($"lookup success in international edition conceptId={conceptId}");
            return result;
        }

        Log.Info($"not found in international edition, trying without version conceptId={conceptId}");

        // Step 2: Try without version (lets server find in any edition)
        result = await LookupDefaultEditionAsync(conceptId);
        if (result != null)
        {
            _cache[conceptId] = (result, DateTimeOffset.UtcNow);
            Log.Info($"lookup success (no version) conceptId={conceptId} edition={result.Branch}");
            return result;
        }

        Log.Info($"not found without version, trying all editions conceptId={conceptId}");

        // Step 3: Get all editions and search (fallback for edge cases)
        var editions = await GetCachedOrFetchEditionsAsync();

        foreach (var edition in editions)
        {
            result = await TryLookupInEditionAsync(conceptId, edition.ModuleId);
            if (result != null)
            {
                _cache[conceptId] = (result, DateTimeOffset.UtcNow);
                Log.Info($"lookup success conceptId={conceptId} edition={result.Branch}");
                return result;
            }
        }

        Log.Error($"concept not found in any edition conceptId={conceptId}");
        throw new Exception($"Concept {conceptId} not found in any SNOMED CT edition");
    }

    /// <summary>
    /// Attempts to look up a concept without specifying a version (server resolves to any available edition).
    /// </summary>
    /// <summary>
    /// Looks up a concept without specifying a version — the server returns the
    /// preferred term from its default edition (e.g., SCTAU for Australian servers).
    /// Use this when you need edition-appropriate display terms.
    /// </summary>
    public async Task<ConceptResult?> LookupDefaultEditionAsync(string conceptId)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["system"] = "http://snomed.info/sct";
        query["code"] = conceptId;
        query["_format"] = "json";

        var builder = new UriBuilder(new Uri(_baseUrl, "CodeSystem/$lookup"))
        {
            Query = query.ToString()
        };
        var url = builder.Uri;

        Log.Debug($"GET {url}");

        try
        {
            using var resp = await GetWithRetryAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log.Debug($"concept not found (no version) conceptId={conceptId}");
                return null;
            }

            if (!resp.IsSuccessStatusCode)
            {
                Log.Error($"HTTP {(int)resp.StatusCode} url={url}");
                return null;
            }

            var parameters = JsonSerializer.Deserialize<FhirParameters>(body, JsonOpts);

            // Extract edition from response version parameter
            string? version = null;
            string? moduleId = null;
            foreach (var param in parameters?.Parameter ?? Array.Empty<FhirParameter>())
            {
                if (param.Name == "version")
                    version = param.ValueString;
            }

            // Parse module ID from version string (http://snomed.info/sct/32506021000036107/version/...)
            if (!string.IsNullOrEmpty(version))
            {
                var parts = version.Split('/');
                if (parts.Length >= 4)
                    moduleId = parts[3];
            }

            return ParseConceptFromParameters(parameters, conceptId, moduleId ?? "unknown");
        }
        catch (Exception ex)
        {
            Log.Error($"lookup error (no version) conceptId={conceptId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Attempts to look up a concept in a specific edition.
    /// </summary>
    private async Task<ConceptResult?> TryLookupInEditionAsync(string conceptId, string editionId)
    {
        var version = $"http://snomed.info/sct/{editionId}";
        var url = BuildLookupUrl(conceptId, "http://snomed.info/sct", version);

        Log.Debug($"GET {url}");

        try
        {
            using var resp = await GetWithRetryAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Log.Debug($"concept not found in edition={editionId} conceptId={conceptId}");
                return null;
            }

            if (!resp.IsSuccessStatusCode)
            {
                Log.Error($"HTTP {(int)resp.StatusCode} url={url}");
                return null;
            }

            var parameters = JsonSerializer.Deserialize<FhirParameters>(body, JsonOpts);
            return ParseConceptFromParameters(parameters, conceptId, editionId);
        }
        catch (Exception ex)
        {
            Log.Error($"lookup error edition={editionId} conceptId={conceptId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns cached editions if still valid, otherwise fetches fresh from the server.
    /// </summary>
    private async Task<List<EditionInfo>> GetCachedOrFetchEditionsAsync()
    {
        if (s_cachedEditions != null && DateTimeOffset.UtcNow - s_editionsCacheTime < EditionsCacheTtl)
        {
            return s_cachedEditions;
        }

        var editions = await FetchEditionsAsync();
        s_cachedEditions = editions;
        s_editionsCacheTime = DateTimeOffset.UtcNow;
        return editions;
    }

    /// <summary>
    /// Fetches available SNOMED CT editions from the server.
    /// </summary>
    private async Task<List<EditionInfo>> FetchEditionsAsync()
    {
        var url = new Uri(_baseUrl, "CodeSystem?url=http://snomed.info/sct,http://snomed.info/xsct&_count=200&_format=json");
        Log.Debug($"GET {url}");

        try
        {
            using var resp = await GetWithRetryAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Log.Error($"failed to fetch editions: HTTP {(int)resp.StatusCode}");
                return new List<EditionInfo>();
            }

            var bundle = JsonSerializer.Deserialize<FhirBundle>(body, JsonOpts);
            var editions = new List<EditionInfo>();
            var seenModules = new HashSet<string>();

            foreach (var entry in bundle?.Entry ?? Array.Empty<FhirBundleEntry>())
            {
                var cs = entry.Resource;
                if (cs?.Version == null) continue;

                var moduleId = ExtractModuleIdFromVersion(cs.Version);
                if (moduleId == null || moduleId == InternationalEditionId) continue;
                if (seenModules.Contains(moduleId)) continue;
                seenModules.Add(moduleId);

                editions.Add(new EditionInfo
                {
                    ModuleId = moduleId,
                    Title = GetEditionName(moduleId) ?? cs.Title ?? cs.Name ?? moduleId
                });
            }

            Log.Info($"found {editions.Count} SNOMED editions");
            return editions;
        }
        catch (Exception ex)
        {
            Log.Error($"failed to fetch editions: {ex.Message}");
            return new List<EditionInfo>();
        }
    }

    /// <summary>
    /// Builds the CodeSystem/$lookup URL.
    /// </summary>
    private Uri BuildLookupUrl(string code, string system, string version)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["system"] = system;
        query["version"] = version;
        query["code"] = code;
        query["_format"] = "json";

        var builder = new UriBuilder(new Uri(_baseUrl, "CodeSystem/$lookup"))
        {
            Query = query.ToString()
        };
        return builder.Uri;
    }

    /// <summary>
    /// Builds the CodeSystem/$lookup URL for a specific code system (without version).
    /// </summary>
    private Uri BuildLookupUrlForSystem(string code, string system)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["system"] = system;
        query["code"] = code;
        query["_format"] = "json";

        var builder = new UriBuilder(new Uri(_baseUrl, "CodeSystem/$lookup"))
        {
            Query = query.ToString()
        };
        return builder.Uri;
    }

    /// <summary>
    /// Parses a ConceptResult from FHIR Parameters response.
    /// </summary>
    private ConceptResult? ParseConceptFromParameters(FhirParameters? parameters, string conceptId, string editionId)
    {
        if (parameters?.Parameter == null) return null;

        string? display = null;
        string? fsn = null;
        bool? active = null;
        string? effectiveTime = null;
        string? moduleId = null;
        string? version = null;

        foreach (var param in parameters.Parameter)
        {
            switch (param.Name)
            {
                case "version":
                    version = param.ValueString;
                    break;
                case "display":
                    display = param.ValueString;
                    break;
                case "property":
                    ParseProperty(param.Part, ref active, ref effectiveTime, ref moduleId);
                    break;
                case "designation":
                    ParseDesignation(param.Part, ref fsn);
                    break;
            }
        }

        var branch = ExtractEditionName(editionId, version);

        return new ConceptResult(
            conceptId,
            branch,
            fsn,
            display,
            active,
            effectiveTime,
            moduleId
        );
    }

    /// <summary>
    /// Parses a ConceptResult from FHIR Parameters response for non-SNOMED code systems.
    /// </summary>
    private ConceptResult? ParseConceptFromParametersGeneric(FhirParameters? parameters, string code, string system)
    {
        if (parameters?.Parameter == null) return null;

        string? display = null;
        string? version = null;

        foreach (var param in parameters.Parameter)
        {
            switch (param.Name)
            {
                case "version":
                    version = param.ValueString;
                    break;
                case "display":
                    display = param.ValueString;
                    break;
            }
        }

        var systemName = new ConceptResult(code, "", null, null, null, null, null, system).SystemName;
        var branch = version != null ? $"{systemName} ({version})" : systemName;

        return new ConceptResult(
            code,
            branch,
            null, // FSN is SNOMED-specific
            display,
            true, // Most code systems don't have active/inactive status
            null,
            null,
            system
        );
    }

    private void ParseProperty(FhirParameterPart[]? parts, ref bool? active, ref string? effectiveTime, ref string? moduleId)
    {
        if (parts == null) return;

        string? propCode = null;
        string? propValueString = null;
        bool? propValueBoolean = null;

        foreach (var part in parts)
        {
            if (part.Name == "code")
                propCode = part.ValueCode;
            else if (part.Name == "value")
            {
                propValueString = part.ValueString ?? part.ValueCode;
                propValueBoolean = part.ValueBoolean;
            }
        }

        if (propCode == null) return;

        switch (propCode)
        {
            case "inactive":
                if (propValueBoolean.HasValue)
                    active = !propValueBoolean.Value;
                else if (propValueString != null)
                    active = propValueString != "true";
                break;
            case "effectiveTime":
                effectiveTime = propValueString;
                break;
            case "moduleId":
                moduleId = propValueString;
                break;
        }
    }

    private void ParseDesignation(FhirParameterPart[]? parts, ref string? fsn)
    {
        if (parts == null) return;

        string? use = null;
        string? value = null;

        foreach (var part in parts)
        {
            if (part.Name == "use")
                use = part.ValueCoding?.Code;
            else if (part.Name == "value")
                value = part.ValueString;
        }

        if (use == FsnDesignationCode)
            fsn = value;
    }

    /// <summary>
    /// Extracts the module ID from a version URI.
    /// </summary>
    private string? ExtractModuleIdFromVersion(string version)
    {
        // Format: http://snomed.info/sct/32506021000036107/version/20240101
        var parts = version.Split('/');
        if (parts.Length >= 4)
            return parts[3];
        return null;
    }

    /// <summary>
    /// Extracts a human-readable edition name.
    /// </summary>
    private string ExtractEditionName(string editionId, string? version)
    {
        var name = GetEditionName(editionId) ?? editionId;

        // Extract date from version if available
        if (!string.IsNullOrEmpty(version))
        {
            var parts = version.Split('/');
            if (parts.Length >= 6)
                return $"{name} ({parts[5]})";
        }

        return name;
    }

    /// <summary>
    /// Maps edition module IDs to human-readable names.
    /// </summary>
    private static string? GetEditionName(string editionId) => editionId switch
    {
        "900000000000207008" => "International",
        "11000221109" => "Argentine",
        "32506021000036107" => "Australian",
        "11000234105" => "Austrian",
        "11000172109" => "Belgian",
        "20611000087101" => "Canadian",
        "554471000005108" => "Danish",
        "11000181102" => "Estonian",
        "11000315107" => "French",
        "11000274103" => "German",
        "11000220105" => "Irish",
        "11000318109" => "Jamaican",
        "450829007" => "Latin American Spanish",
        "11000146104" => "Netherlands",
        "21000210109" => "New Zealand",
        "51000202101" => "Norwegian",
        "900000001000122104" => "Spanish",
        "45991000052106" => "Swedish",
        "2011000195101" => "Swiss",
        "83821000000107" => "UK Composition",
        "999000041000000102" => "United Kingdom",
        "999000011000000103" => "UK Clinical",
        "999000021000000109" => "UK Drug",
        "731000124108" => "United States",
        "5631000179106" => "Uruguayan",
        _ => null
    };

    /// <summary>
    /// Looks up a SNOMED CT concept with full properties for visualization.
    /// Returns parent concepts, defining relationships, and definition status.
    /// </summary>
    /// <param name="conceptId">The SNOMED CT concept ID</param>
    /// <returns>Properties including parents, attributes, and definition status</returns>
    public async Task<ConceptProperties?> LookupWithPropertiesAsync(string conceptId)
    {
        Log.Info($"property lookup conceptId={conceptId}");

        // Try International edition first, then without version (server resolves edition)
        var versions = new string?[]
        {
            $"http://snomed.info/sct/{InternationalEditionId}",
            null // no version — lets the server find the concept in any edition
        };

        foreach (var version in versions)
        {
            var result = await TryPropertyLookupAsync(conceptId, version);
            if (result != null)
            {
                Log.Info($"property lookup success conceptId={conceptId} version={version ?? "any"}");
                return result;
            }
        }

        Log.Error($"property lookup not found conceptId={conceptId}");
        return null;
    }

    /// <summary>
    /// Attempts a property lookup for a concept with an optional version.
    /// </summary>
    private async Task<ConceptProperties?> TryPropertyLookupAsync(string conceptId, string? version)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["system"] = "http://snomed.info/sct";
        query["code"] = conceptId;
        query["property"] = "*";
        query["_format"] = "json";
        if (version != null)
            query["version"] = version;

        var builder = new UriBuilder(new Uri(_baseUrl, "CodeSystem/$lookup"))
        {
            Query = query.ToString()
        };
        var url = builder.Uri;

        Log.Debug($"GET {url}");

        try
        {
            using var resp = await GetWithRetryAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Log.Debug($"property lookup HTTP {(int)resp.StatusCode} version={version ?? "any"}");
                return null;
            }

            var parameters = JsonSerializer.Deserialize<FhirParameters>(body, JsonOpts);
            return ParseConceptProperties(parameters, conceptId);
        }
        catch (Exception ex)
        {
            Log.Debug($"property lookup error version={version ?? "any"}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parses concept properties from a FHIR Parameters response with property=*.
    /// </summary>
    private ConceptProperties ParseConceptProperties(FhirParameters? parameters, string conceptId)
    {
        var props = new ConceptProperties { ConceptId = conceptId };

        if (parameters?.Parameter == null) return props;

        foreach (var param in parameters.Parameter)
        {
            switch (param.Name)
            {
                case "display":
                    props.PreferredTerm = param.ValueString;
                    break;
                case "property":
                    ParseConceptProperty(param.Part, props);
                    break;
                case "designation":
                    ParseDesignationForProperties(param.Part, props);
                    break;
            }
        }

        return props;
    }

    private void ParseConceptProperty(FhirParameterPart[]? parts, ConceptProperties props)
    {
        if (parts == null) return;

        string? code = null;
        string? valueString = null;
        string? valueCode = null;
        bool? valueBoolean = null;
        string? subPropertyValue = null;

        foreach (var part in parts)
        {
            switch (part.Name)
            {
                case "code":
                    code = part.ValueCode ?? part.ValueString;
                    break;
                case "value":
                    valueString = part.ValueString;
                    valueCode = part.ValueCode;
                    valueBoolean = part.ValueBoolean;
                    break;
                case "description":
                    // The description of the property value (used for concept display terms)
                    subPropertyValue = part.ValueString;
                    break;
                case "subproperty":
                    // Sub-properties contain relationship details (e.g., attribute type → value concept)
                    ParseSubproperty(part.Part, props);
                    break;
            }
        }

        if (code == null) return;

        switch (code)
        {
            case "parent":
                var parentCode = valueCode ?? valueString;
                if (parentCode != null)
                    props.ParentCodes.Add(new ConceptPropertyValue(parentCode, subPropertyValue));
                break;
            case "child":
                // We don't use children for visualization currently
                break;
            case "sufficientlyDefined":
                if (valueBoolean.HasValue)
                    props.SufficientlyDefined = valueBoolean.Value;
                else if (valueString != null)
                    props.SufficientlyDefined = valueString == "true";
                break;
            case "normalForm":
            case "normalFormTerse":
                props.NormalForm ??= valueString;
                break;
            case "inactive":
                if (valueBoolean.HasValue)
                    props.Active = !valueBoolean.Value;
                break;
            default:
                // Relationship properties — map attribute type ID to value concept ID
                var relValue = valueCode ?? valueString;
                if (relValue != null && relValue.All(char.IsDigit) && relValue.Length >= 6)
                    props.RelationshipProperties[code] = relValue;
                break;
        }
    }

    /// <summary>
    /// Parses a subproperty to extract relationship attribute type → value mappings.
    /// These give pre-coordinated concept IDs for values that the normal form decomposes
    /// (e.g., 999000031000168102 = 258798001 for mg/mL).
    /// </summary>
    private static void ParseSubproperty(FhirParameterPart[]? parts, ConceptProperties props)
    {
        if (parts == null) return;

        string? code = null;
        string? value = null;

        foreach (var part in parts)
        {
            switch (part.Name)
            {
                case "code":
                    code = part.ValueCode ?? part.ValueString;
                    break;
                case "value":
                    value = part.ValueCode ?? part.ValueString;
                    break;
            }
        }

        if (code != null && value != null && value.All(char.IsDigit) && value.Length >= 6)
        {
            props.RelationshipProperties[code] = value;
        }
    }

    private void ParseDesignationForProperties(FhirParameterPart[]? parts, ConceptProperties props)
    {
        if (parts == null) return;

        string? use = null;
        string? value = null;

        foreach (var part in parts)
        {
            if (part.Name == "use")
                use = part.ValueCoding?.Code;
            else if (part.Name == "value")
                value = part.ValueString;
        }

        if (use == FsnDesignationCode)
            props.FullySpecifiedName = value;
    }

    /// <summary>
    /// Searches for concepts matching a text filter using ValueSet/$expand.
    /// </summary>
    /// <param name="filter">The search text (minimum 3 characters)</param>
    /// <param name="system">The code system URI to search (e.g., "http://snomed.info/sct")</param>
    /// <param name="editionUri">Optional SNOMED CT edition URI for filtering (e.g., "http://snomed.info/sct/32506021000036107")</param>
    /// <param name="ct">Cancellation token for aborting the request</param>
    /// <returns>List of matching search results (up to 50)</returns>
    public async Task<List<SearchResultItem>> SearchAsync(string filter, string system, string? editionUri = null, System.Threading.CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Length < 3)
            return new List<SearchResultItem>();

        Log.Info($"search filter='{filter}' system={system} edition={editionUri ?? "all"}");

        var url = new Uri(_baseUrl, "ValueSet/$expand");

        // Build the request body
        var body = BuildSearchRequestBody(filter, system, editionUri);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/fhir+json");

        try
        {
            using var resp = await _http.PostAsync(url, content, ct);
            var responseBody = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                Log.Error($"search failed: HTTP {(int)resp.StatusCode}");
                return new List<SearchResultItem>();
            }

            var valueSet = JsonSerializer.Deserialize<FhirValueSetResponse>(responseBody, JsonOpts);
            return ParseSearchResults(valueSet, system);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("search cancelled");
            return new List<SearchResultItem>();
        }
        catch (Exception ex)
        {
            Log.Error($"search error: {ex.Message}");
            return new List<SearchResultItem>();
        }
    }

    /// <summary>
    /// Looks up multiple SNOMED CT concepts in a single batch request using ValueSet/$expand.
    /// This is significantly faster than individual lookups (~15x speedup for 50+ concepts).
    /// </summary>
    /// <param name="conceptIds">The SNOMED CT concept IDs to look up</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>A BatchLookupResult containing PT, FSN, and active status for each found concept</returns>
    public async Task<BatchLookupResult> BatchLookupAsync(IEnumerable<string> conceptIds, CancellationToken ct = default)
    {
        var codes = conceptIds.Distinct().ToList();
        if (codes.Count == 0)
            return new BatchLookupResult(new(), new(), new());

        Log.Info($"batch lookup count={codes.Count}");

        // Step 1: Fast batch lookup against default edition (no version = server default)
        var ptByCode = new Dictionary<string, string>();
        var fsnByCode = new Dictionary<string, string>();
        var activeByCode = new Dictionary<string, bool>();

        var url = new Uri(_baseUrl, "ValueSet/$expand");
        var body = BuildBatchLookupRequestBody(codes);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/fhir+json");

        try
        {
            using var resp = await _http.PostAsync(url, content, ct);
            var responseBody = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
            {
                var valueSet = JsonSerializer.Deserialize<FhirValueSetResponse>(responseBody, JsonOpts);
                var defaultResults = ParseBatchLookupResults(valueSet);
                foreach (var kv in defaultResults.PtByCode) ptByCode[kv.Key] = kv.Value;
                foreach (var kv in defaultResults.FsnByCode) fsnByCode[kv.Key] = kv.Value;
                foreach (var kv in defaultResults.ActiveByCode) activeByCode[kv.Key] = kv.Value;
            }
            else
            {
                Log.Error($"batch lookup failed: HTTP {(int)resp.StatusCode}");
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug("batch lookup cancelled");
            return new BatchLookupResult(new(), new(), new());
        }
        catch (Exception ex)
        {
            Log.Error($"batch lookup default edition error: {ex.Message}");
        }

        // Step 2: For codes not found in the default edition, try other editions in parallel.
        // Skip core (non-namespaced) SCTIDs — they're International Edition only, so if the
        // default edition didn't find them, other editions won't either.
        var notFoundIds = codes.Where(c => !ptByCode.ContainsKey(c) && !SCTIDValidator.IsCoreSCTID(c)).ToList();
        if (notFoundIds.Count > 0)
        {
            Log.Info($"batch lookup {notFoundIds.Count} namespaced codes not in default edition, trying other editions");
            try
            {
                var editions = await GetCachedOrFetchEditionsAsync();
                if (editions.Count > 0)
                {
                    var editionTasks = notFoundIds.Select(async conceptId =>
                    {
                        foreach (var edition in editions)
                        {
                            var result = await TryLookupInEditionAsync(conceptId, edition.ModuleId);
                            if (result != null) return (conceptId, result);
                        }
                        return (conceptId, (ConceptResult?)null);
                    });

                    var editionResults = await Task.WhenAll(editionTasks);
                    foreach (var (conceptId, result) in editionResults)
                    {
                        if (result == null) continue;
                        if (result.Pt != null) ptByCode[conceptId] = result.Pt;
                        if (result.Fsn != null) fsnByCode[conceptId] = result.Fsn;
                        if (result.Active.HasValue) activeByCode[conceptId] = result.Active.Value;
                        _cache[conceptId] = (result, DateTimeOffset.UtcNow);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"batch lookup edition fallback error: {ex.Message}");
            }
        }

        Log.Info($"batch lookup found {ptByCode.Count} concepts total");
        return new BatchLookupResult(ptByCode, fsnByCode, activeByCode);
    }

    private string BuildBatchLookupRequestBody(List<string> codes)
    {
        var conceptArray = codes.Select(c => new Dictionary<string, string> { ["code"] = c }).ToArray();

        var parameters = new Dictionary<string, object>
        {
            ["resourceType"] = "Parameters",
            ["parameter"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = "valueSet",
                    ["resource"] = new Dictionary<string, object>
                    {
                        ["resourceType"] = "ValueSet",
                        ["compose"] = new Dictionary<string, object>
                        {
                            ["include"] = new[]
                            {
                                new Dictionary<string, object>
                                {
                                    ["system"] = "http://snomed.info/sct",
                                    ["concept"] = conceptArray
                                }
                            }
                        }
                    }
                },
                new Dictionary<string, object> { ["name"] = "count", ["valueInteger"] = codes.Count + 10 },
                new Dictionary<string, object> { ["name"] = "includeDesignations", ["valueBoolean"] = true },
                new Dictionary<string, object>
                {
                    ["name"] = "property",
                    ["valueString"] = "inactive"
                }
            }
        };

        return JsonSerializer.Serialize(parameters, JsonOpts);
    }

    private BatchLookupResult ParseBatchLookupResults(FhirValueSetResponse? valueSet)
    {
        var ptByCode = new Dictionary<string, string>();
        var fsnByCode = new Dictionary<string, string>();
        var activeByCode = new Dictionary<string, bool>();

        var contains = valueSet?.Expansion?.Contains;
        if (contains == null) return new BatchLookupResult(ptByCode, fsnByCode, activeByCode);

        foreach (var item in contains)
        {
            if (item.Code == null) continue;

            // Display is the preferred term
            if (item.Display != null)
                ptByCode[item.Code] = item.Display;

            // Look for FSN in designations
            if (item.Designation != null)
            {
                foreach (var d in item.Designation)
                {
                    if (d.Use?.Code == FsnDesignationCode && d.Value != null)
                    {
                        fsnByCode[item.Code] = d.Value;
                        break;
                    }
                }
            }

            // Check inactive property
            if (item.Inactive.HasValue)
            {
                activeByCode[item.Code] = !item.Inactive.Value;
            }
            else
            {
                // Default to active if not specified
                activeByCode[item.Code] = true;
            }
        }

        Log.Info($"batch lookup returned {ptByCode.Count} results");
        return new BatchLookupResult(ptByCode, fsnByCode, activeByCode);
    }

    /// <summary>
    /// Evaluates an ECL expression and returns matching concepts.
    /// Uses ValueSet/$expand with the ECL expression encoded in an implicit ValueSet URL.
    /// </summary>
    /// <param name="expression">The ECL expression to evaluate</param>
    /// <param name="count">Maximum number of concepts to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>An EvaluationResult with total count and matching concepts</returns>
    public async Task<EvaluationResult> EvaluateEclAsync(string expression, int count = 50, CancellationToken ct = default)
    {
        var trimmed = expression.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return new EvaluationResult(0, []);

        Log.Info($"evaluateECL count={count}");

        var encodedExpression = Uri.EscapeDataString(trimmed);
        var implicitValueSetUrl = $"http://snomed.info/sct?fhir_vs=ecl/{encodedExpression}";

        var parameters = new Dictionary<string, object>
        {
            ["resourceType"] = "Parameters",
            ["parameter"] = new object[]
            {
                new Dictionary<string, object> { ["name"] = "url", ["valueUri"] = implicitValueSetUrl },
                new Dictionary<string, object> { ["name"] = "count", ["valueInteger"] = count },
                new Dictionary<string, object> { ["name"] = "includeDesignations", ["valueBoolean"] = true }
            }
        };

        var url = new Uri(_baseUrl, "ValueSet/$expand");
        var body = JsonSerializer.Serialize(parameters, JsonOpts);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/fhir+json");

        using var resp = await _http.PostAsync(url, content, ct);
        var responseBody = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            Log.Error($"evaluateECL failed: HTTP {(int)resp.StatusCode}");
            throw new Exception($"ECL evaluation failed: HTTP {(int)resp.StatusCode}");
        }

        var valueSet = JsonSerializer.Deserialize<FhirValueSetResponse>(responseBody, JsonOpts);
        var total = valueSet?.Expansion?.Total ?? 0;

        var concepts = new List<EvaluationConcept>();
        foreach (var entry in valueSet?.Expansion?.Contains ?? [])
        {
            if (entry.Code == null || entry.Display == null) continue;

            string? fsn = null;
            if (entry.Designation != null)
            {
                foreach (var d in entry.Designation)
                {
                    if (d.Use?.Code == FsnDesignationCode && d.Value != null)
                    {
                        fsn = d.Value;
                        break;
                    }
                }
            }

            concepts.Add(new EvaluationConcept(entry.Code, entry.Display, fsn));
        }

        Log.Info($"evaluateECL total={total} returned={concepts.Count}");
        return new EvaluationResult(total, concepts);
    }

    private string BuildSearchRequestBody(string filter, string system, string? editionUri)
    {
        var systemUrl = system;
        var version = editionUri;

        // Build include with optional version for edition filtering
        var includeObj = new Dictionary<string, object> { ["system"] = systemUrl };
        if (!string.IsNullOrEmpty(version))
            includeObj["version"] = version;

        var parameters = new Dictionary<string, object>
        {
            ["resourceType"] = "Parameters",
            ["parameter"] = new object[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = "valueSet",
                    ["resource"] = new Dictionary<string, object>
                    {
                        ["resourceType"] = "ValueSet",
                        ["compose"] = new Dictionary<string, object>
                        {
                            ["include"] = new[] { includeObj }
                        }
                    }
                },
                new Dictionary<string, object> { ["name"] = "filter", ["valueString"] = filter },
                new Dictionary<string, object> { ["name"] = "count", ["valueInteger"] = 50 },
                new Dictionary<string, object> { ["name"] = "includeDesignations", ["valueBoolean"] = true }
            }
        };

        return JsonSerializer.Serialize(parameters, JsonOpts);
    }

    private List<SearchResultItem> ParseSearchResults(FhirValueSetResponse? valueSet, string system)
    {
        var results = new List<SearchResultItem>();
        var contains = valueSet?.Expansion?.Contains;
        if (contains == null) return results;

        var systemName = new ConceptResult("", "", null, null, null, null, null, system).SystemName;

        foreach (var item in contains)
        {
            if (item.Code == null) continue;

            string? fsn = null;
            // Look for FSN in designations
            if (item.Designation != null)
            {
                foreach (var d in item.Designation)
                {
                    if (d.Use?.Code == FsnDesignationCode)
                    {
                        fsn = d.Value;
                        break;
                    }
                }
            }

            results.Add(new SearchResultItem(
                item.Code,
                item.Display,
                fsn,
                systemName,
                item.System ?? system
            ));
        }

        Log.Info($"search returned {results.Count} results");
        return results;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class EditionInfo
    {
        public string ModuleId { get; set; } = "";
        public string Title { get; set; } = "";
    }

    /// <summary>
    /// Gets available SNOMED CT editions from the server, suitable for populating a UI picker.
    /// Returns editions sorted alphabetically by title, with International always first.
    /// </summary>
    /// <returns>List of available editions as (title, editionUri) pairs</returns>
    public async Task<List<SnomedEdition>> GetAvailableEditionsAsync()
    {
        var editions = new List<SnomedEdition>();

        try
        {
            var raw = await GetCachedOrFetchEditionsAsync();

            // Always include International first
            editions.Add(new SnomedEdition("International", $"http://snomed.info/sct/{InternationalEditionId}"));

            // Add the rest, sorted alphabetically
            var sorted = raw
                .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var e in sorted)
            {
                editions.Add(new SnomedEdition(e.Title, $"http://snomed.info/sct/{e.ModuleId}"));
            }

            Log.Info($"GetAvailableEditionsAsync returned {editions.Count} editions");
        }
        catch (Exception ex)
        {
            Log.Error($"GetAvailableEditionsAsync failed: {ex.Message}");
            // Return at least International on failure
            if (editions.Count == 0)
            {
                editions.Add(new SnomedEdition("International", $"http://snomed.info/sct/{InternationalEditionId}"));
            }
        }

        return editions;
    }
}

/// <summary>
/// Represents an available code system from the FHIR server.
/// </summary>
/// <param name="Uri">The code system URI</param>
/// <param name="Title">The display title</param>
/// <param name="Version">The version string, if available</param>
public record AvailableCodeSystem(string Uri, string Title, string? Version);

/// <summary>
/// Represents a SNOMED CT edition available on the server.
/// </summary>
/// <param name="Title">Human-readable edition name (e.g., "Australian")</param>
/// <param name="EditionUri">The edition URI for search filtering (e.g., "http://snomed.info/sct/32506021000036107")</param>
public record SnomedEdition(string Title, string EditionUri);

/// <summary>
/// A single search result from ValueSet/$expand.
/// </summary>
/// <param name="Code">The concept code</param>
/// <param name="Display">The preferred term / display name</param>
/// <param name="Fsn">The fully specified name (SNOMED CT only, null for other systems)</param>
/// <param name="SystemName">Human-readable code system name</param>
/// <param name="SystemUri">The code system URI</param>
public record SearchResultItem(
    string Code,
    string? Display,
    string? Fsn,
    string SystemName,
    string SystemUri
);

/// <summary>
/// Full concept properties including parents, normal form, and definition status.
/// </summary>
public sealed class ConceptProperties
{
    public string ConceptId { get; set; } = "";
    public string? PreferredTerm { get; set; }
    public string? FullySpecifiedName { get; set; }
    public bool SufficientlyDefined { get; set; }
    public bool? Active { get; set; }
    public string? NormalForm { get; set; }
    public List<ConceptPropertyValue> ParentCodes { get; } = new();

    /// <summary>
    /// Relationship properties: maps attribute type concept ID to value concept ID.
    /// These are the pre-coordinated values from the FHIR property response,
    /// which may differ from the decomposed normal form (e.g., 258798001 "mg/mL"
    /// instead of the postcoordinated expression 415777001:{numerator,denominator}).
    /// </summary>
    public Dictionary<string, string> RelationshipProperties { get; } = new();
}

/// <summary>
/// A concept code with its display term from a property response.
/// </summary>
public sealed record ConceptPropertyValue(string Code, string? Display);

/// <summary>
/// Result of a batch concept lookup containing PT, FSN, and active status indexed by concept code.
/// </summary>
/// <param name="PtByCode">Preferred terms keyed by concept code</param>
/// <param name="FsnByCode">Fully specified names keyed by concept code</param>
/// <param name="ActiveByCode">Active status keyed by concept code (true = active)</param>
public record BatchLookupResult(
    Dictionary<string, string> PtByCode,
    Dictionary<string, string> FsnByCode,
    Dictionary<string, bool> ActiveByCode
);

// FHIR Data Structures

internal sealed class FhirParameters
{
    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("parameter")]
    public FhirParameter[]? Parameter { get; set; }
}

internal sealed class FhirParameter
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("valueString")]
    public string? ValueString { get; set; }

    [JsonPropertyName("valueCode")]
    public string? ValueCode { get; set; }

    [JsonPropertyName("part")]
    public FhirParameterPart[]? Part { get; set; }
}

internal sealed class FhirParameterPart
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("valueString")]
    public string? ValueString { get; set; }

    [JsonPropertyName("valueCode")]
    public string? ValueCode { get; set; }

    [JsonPropertyName("valueBoolean")]
    public bool? ValueBoolean { get; set; }

    [JsonPropertyName("valueCoding")]
    public FhirCoding? ValueCoding { get; set; }

    [JsonPropertyName("part")]
    public FhirParameterPart[]? Part { get; set; }
}

internal sealed class FhirCoding
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

internal sealed class FhirBundle
{
    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("entry")]
    public FhirBundleEntry[]? Entry { get; set; }
}

internal sealed class FhirBundleEntry
{
    [JsonPropertyName("resource")]
    public FhirCodeSystem? Resource { get; set; }
}

internal sealed class FhirCodeSystem
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

// ValueSet/$expand response DTOs

internal sealed class FhirValueSetResponse
{
    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("expansion")]
    public FhirExpansion? Expansion { get; set; }
}

internal sealed class FhirExpansion
{
    [JsonPropertyName("total")]
    public int? Total { get; set; }

    [JsonPropertyName("contains")]
    public FhirExpansionContains[]? Contains { get; set; }
}

internal sealed class FhirExpansionContains
{
    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("inactive")]
    public bool? Inactive { get; set; }

    [JsonPropertyName("designation")]
    public FhirDesignation[]? Designation { get; set; }
}

internal sealed class FhirDesignation
{
    [JsonPropertyName("use")]
    public FhirCoding? Use { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
