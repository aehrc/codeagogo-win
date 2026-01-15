// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Codeagogo;

/// <summary>
/// ViewModel for the ECL Workbench window.
/// Manages editor state, evaluation lifecycle, and results display.
/// </summary>
public sealed class ECLWorkbenchViewModel : INotifyPropertyChanged
{
    private readonly OntoserverClient _client;
    private string _currentExpression = "";
    private bool _isEvaluating;
    private EvaluationResult? _result;
    private string? _errorMessage;
    private bool _showFsn;
    private CancellationTokenSource? _cts;

    public ECLWorkbenchViewModel(OntoserverClient client)
    {
        _client = client;
    }

    public string CurrentExpression
    {
        get => _currentExpression;
        set => SetField(ref _currentExpression, value);
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

    public bool ShowFsn
    {
        get => _showFsn;
        set => SetField(ref _showFsn, value);
    }

    /// <summary>
    /// Evaluates the given ECL expression. Cancels any in-flight request.
    /// </summary>
    public async Task EvaluateAsync(string expression, int resultLimit = 50)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        CurrentExpression = expression;

        if (string.IsNullOrWhiteSpace(expression))
        {
            Result = null;
            ErrorMessage = null;
            IsEvaluating = false;
            return;
        }

        IsEvaluating = true;
        ErrorMessage = null;

        try
        {
            var result = await _client.EvaluateEclAsync(expression, resultLimit, ct);
            if (!ct.IsCancellationRequested)
            {
                Result = result;
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled by new evaluation — ignore
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                ErrorMessage = ex.Message;
                Log.Error($"ECL workbench evaluation error: {ex.Message}");
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
