// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Codeagogo;

/// <summary>
/// ViewModel for the ECL evaluation window.
/// Manages evaluation state, results, and concept validation warnings.
/// </summary>
public sealed class EvaluateViewModel : INotifyPropertyChanged
{
    private readonly OntoserverClient _client;
    private string _expression = "";
    private bool _isEvaluating;
    private EvaluationResult? _result;
    private string? _errorMessage;
    private List<string> _warnings = [];
    private bool _showFsn;
    private CancellationTokenSource? _cts;

    public EvaluateViewModel(OntoserverClient client)
    {
        _client = client;
    }

    public string Expression
    {
        get => _expression;
        set => SetField(ref _expression, value);
    }

    public bool IsEvaluating
    {
        get => _isEvaluating;
        private set => SetField(ref _isEvaluating, value);
    }

    public EvaluationResult? Result
    {
        get => _result;
        private set => SetField(ref _result, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }

    public List<string> Warnings
    {
        get => _warnings;
        set => SetField(ref _warnings, value);
    }

    public bool ShowFsn
    {
        get => _showFsn;
        set => SetField(ref _showFsn, value);
    }

    /// <summary>
    /// Evaluates the current expression against the FHIR server.
    /// </summary>
    public async Task EvaluateAsync(int resultLimit = 50)
    {
        // Cancel any previous evaluation
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        IsEvaluating = true;
        ErrorMessage = null;
        Result = null;

        try
        {
            var result = await _client.EvaluateEclAsync(Expression, resultLimit, ct);
            if (!ct.IsCancellationRequested)
            {
                Result = result;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled — ignore
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                ErrorMessage = ex.Message;
                Log.Error($"ECL evaluation error: {ex.Message}");
            }
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                IsEvaluating = false;
            }
        }
    }

    /// <summary>
    /// Clears the evaluation state.
    /// </summary>
    public void ClearState()
    {
        _cts?.Cancel();
        Result = null;
        ErrorMessage = null;
        Warnings = [];
        IsEvaluating = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
