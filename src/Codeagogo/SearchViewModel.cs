// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Codeagogo;

/// <summary>
/// ViewModel for the search panel, managing typeahead search with debouncing,
/// code system filtering, and result formatting.
/// </summary>
public sealed class SearchViewModel
{
    private readonly OntoserverClient _client;
    private CancellationTokenSource? _cts;
    private readonly System.Timers.Timer _debounceTimer;

    public ObservableCollection<SearchResultItem> Results { get; } = new();

    public string SelectedCodeSystem { get; set; } = "http://snomed.info/sct";
    public string? SelectedEditionUri { get; set; }
    public InsertFormat SelectedFormat { get; set; }
    public bool IsSearching { get; private set; }

    public SearchViewModel(OntoserverClient client, InsertFormat defaultFormat)
    {
        _client = client;
        SelectedFormat = defaultFormat;

        _debounceTimer = new System.Timers.Timer(300);
        _debounceTimer.AutoReset = false;
        _debounceTimer.Elapsed += async (_, _) =>
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await ExecuteSearchAsync(_pendingQuery);
            });
        };
    }

    private string? _pendingQuery;

    /// <summary>
    /// Triggers a debounced search. Waits 300ms after the last call before executing.
    /// </summary>
    public void SearchDebounced(string query)
    {
        _pendingQuery = query;

        // Cancel any in-flight request
        _cts?.Cancel();
        _debounceTimer.Stop();

        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            Results.Clear();
            return;
        }

        _debounceTimer.Start();
    }

    /// <summary>
    /// Executes the search immediately (called after debounce delay).
    /// </summary>
    private async Task ExecuteSearchAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
        {
            Results.Clear();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        IsSearching = true;

        try
        {
            var results = await _client.SearchAsync(query, SelectedCodeSystem, SelectedEditionUri, token);

            if (token.IsCancellationRequested) return;

            Results.Clear();
            foreach (var item in results)
            {
                Results.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when typing rapidly
        }
        catch (Exception ex)
        {
            Log.Error($"Search error: {ex.Message}");
            Results.Clear();
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Formats a search result for insertion based on the selected format.
    /// </summary>
    public string FormatForInsertion(SearchResultItem item)
    {
        return SelectedFormat switch
        {
            InsertFormat.IdOnly => item.Code,
            InsertFormat.PtOnly => item.Display ?? item.Code,
            InsertFormat.FsnOnly => item.Fsn ?? item.Display ?? item.Code,
            InsertFormat.IdPipePT => $"{item.Code} |{item.Display ?? item.Code}|",
            InsertFormat.IdPipeFSN => $"{item.Code} |{item.Fsn ?? item.Display ?? item.Code}|",
            _ => item.Code
        };
    }

    /// <summary>
    /// Cancels any pending search and disposes resources.
    /// </summary>
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _debounceTimer.Stop();
        _debounceTimer.Dispose();
    }
}
