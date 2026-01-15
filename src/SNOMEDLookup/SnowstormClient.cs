using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace SNOMEDLookup;

public sealed class SnowstormClient
{
    private static readonly Uri BaseUri = new("https://lookup.snomedtools.org/snowstorm/snomed-ct/");
    private readonly HttpClient _http;

    private readonly ConcurrentDictionary<string, (ConceptResult Result, DateTimeOffset Ts)> _cache = new();
    private readonly TimeSpan _ttl = TimeSpan.FromHours(6);

    public SnowstormClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ConceptResult> LookupAsync(string conceptId)
    {
        if (_cache.TryGetValue(conceptId, out var hit) && DateTimeOffset.UtcNow - hit.Ts < _ttl)
        {
            Log.Debug($"cache hit conceptId={conceptId}");
            return hit.Result;
        }

        var branch = await ResolveBranchAsync(conceptId);
        var detail = await FetchConceptAsync(branch, conceptId);

        var result = new ConceptResult(
            conceptId,
            branch,
            detail.fsn?.term,
            detail.pt?.term,
            detail.active,
            detail.effectiveTime,
            detail.moduleId
        );

        _cache[conceptId] = (result, DateTimeOffset.UtcNow);
        return result;
    }

    private async Task<string> ResolveBranchAsync(string conceptId)
    {
        var url = new Uri(BaseUri, $"multisearch/concepts?conceptIds={Uri.EscapeDataString(conceptId)}");
        Log.Info($"GET {url}");

        var ms = await GetJsonAsync<MultiSearchResponse>(url);
        var branch = ms.items != null && ms.items.Length > 0 ? ms.items[0].branch : null;

        if (string.IsNullOrWhiteSpace(branch))
            throw new ConceptNotFoundException("Concept not found");

        return branch!;
    }

    private async Task<ConceptDetail> FetchConceptAsync(string branch, string conceptId)
    {
        var safeBranch = string.Join("/", branch.Split('/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var url = new Uri(BaseUri, $"browser/{safeBranch}/concepts/{Uri.EscapeDataString(conceptId)}");
        Log.Info($"GET {url}");

        return await GetJsonAsync<ConceptDetail>(url);
    }

    private async Task<T> GetJsonAsync<T>(Uri url)
    {
        try
        {
            using var resp = await _http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            Log.Info($"HTTP {(int)resp.StatusCode} {url}");

            if (!resp.IsSuccessStatusCode)
            {
                // Handle specific HTTP status codes with user-friendly messages
                throw (int)resp.StatusCode switch
                {
                    429 => new RateLimitException("Too many requests. Please wait a moment and try again."),
                    404 => new ConceptNotFoundException("Concept not found in the terminology server."),
                    503 => new ApiException("Terminology server is temporarily unavailable. Please try again later."),
                    _ => new ApiException($"Server error ({(int)resp.StatusCode}). Please try again later.")
                };
            }

            return JsonSerializer.Deserialize<T>(body, JsonOpts) ?? throw new ApiException("Empty response from server");
        }
        catch (HttpRequestException ex)
        {
            Log.Error($"HttpRequestException url={url} msg={ex.Message}");
            throw new ApiException("Network error. Please check your internet connection.", ex);
        }
        catch (TaskCanceledException ex)
        {
            Log.Error($"Timeout url={url} msg={ex.Message}");
            throw new ApiException("Request timed out. Please try again.", ex);
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class MultiSearchResponse
    {
        public Item[]? items { get; set; }
        public sealed class Item
        {
            public string? branch { get; set; }
        }
    }

    private sealed class ConceptDetail
    {
        public string? conceptId { get; set; }
        public TermObj? fsn { get; set; }
        public TermObj? pt { get; set; }
        public bool? active { get; set; }
        public string? effectiveTime { get; set; }
        public string? moduleId { get; set; }

        public sealed class TermObj
        {
            public string? term { get; set; }
            public string? lang { get; set; }
        }
    }
}

// Custom exception types for better error handling
public class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
    public ApiException(string message, Exception innerException) : base(message, innerException) { }
}

public class RateLimitException : ApiException
{
    public RateLimitException(string message) : base(message) { }
}

public class ConceptNotFoundException : ApiException
{
    public ConceptNotFoundException(string message) : base(message) { }
}
