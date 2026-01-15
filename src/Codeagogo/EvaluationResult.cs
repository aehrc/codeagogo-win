// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Result of evaluating an ECL expression against a FHIR terminology server.
/// </summary>
/// <param name="Total">Total number of matching concepts (may exceed returned count)</param>
/// <param name="Concepts">The returned concept list (may be truncated by the result limit)</param>
public sealed record EvaluationResult(int Total, List<EvaluationConcept> Concepts);

/// <summary>
/// A concept returned from ECL evaluation.
/// </summary>
public sealed record EvaluationConcept(string Code, string Display, string? Fsn)
{
    /// <summary>
    /// Extracts the semantic tag from the FSN (e.g., "disorder" from "Diabetes mellitus (disorder)").
    /// Returns null if no FSN or no parenthesised suffix.
    /// </summary>
    public string? SemanticTag
    {
        get
        {
            if (string.IsNullOrEmpty(Fsn)) return null;
            var lastOpen = Fsn.LastIndexOf('(');
            var lastClose = Fsn.LastIndexOf(')');
            if (lastOpen < 0 || lastClose <= lastOpen) return null;
            return Fsn[(lastOpen + 1)..lastClose];
        }
    }
}
