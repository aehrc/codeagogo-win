// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for ECL evaluation models, settings, and concept validation.
/// </summary>
public class EvaluateTests
{
    #region EvaluationConcept SemanticTag

    [Fact]
    public void SemanticTag_ExtractsFromFSN()
    {
        var concept = new EvaluationConcept("73211009", "Diabetes mellitus", "Diabetes mellitus (disorder)");
        concept.SemanticTag.Should().Be("disorder");
    }

    [Fact]
    public void SemanticTag_ExtractsFromFSNWithNestedParens()
    {
        var concept = new EvaluationConcept("123", "Test", "Some (complex) concept (finding)");
        concept.SemanticTag.Should().Be("finding");
    }

    [Fact]
    public void SemanticTag_NullWhenNoFSN()
    {
        var concept = new EvaluationConcept("73211009", "Diabetes mellitus", null);
        concept.SemanticTag.Should().BeNull();
    }

    [Fact]
    public void SemanticTag_NullWhenEmptyFSN()
    {
        var concept = new EvaluationConcept("73211009", "Diabetes mellitus", "");
        concept.SemanticTag.Should().BeNull();
    }

    [Fact]
    public void SemanticTag_NullWhenNoParentheses()
    {
        var concept = new EvaluationConcept("73211009", "Diabetes mellitus", "Diabetes mellitus");
        concept.SemanticTag.Should().BeNull();
    }

    [Fact]
    public void SemanticTag_NullWhenMismatchedParens()
    {
        var concept = new EvaluationConcept("73211009", "Test", "Test (disorder");
        concept.SemanticTag.Should().BeNull();
    }

    [Theory]
    [InlineData("Diabetes mellitus (disorder)", "disorder")]
    [InlineData("Paracetamol (substance)", "substance")]
    [InlineData("Heart rate (observable entity)", "observable entity")]
    [InlineData("Left arm (body structure)", "body structure")]
    public void SemanticTag_CommonTags(string fsn, string expectedTag)
    {
        var concept = new EvaluationConcept("123", "Test", fsn);
        concept.SemanticTag.Should().Be(expectedTag);
    }

    #endregion

    #region EvaluationResult

    [Fact]
    public void EvaluationResult_Properties()
    {
        var concepts = new List<EvaluationConcept> { new("1", "A", null), new("2", "B", "B (disorder)") };
        var result = new EvaluationResult(10, concepts);
        result.Total.Should().Be(10);
        result.Concepts.Should().HaveCount(2);
        result.Concepts[1].SemanticTag.Should().Be("disorder");
    }

    [Fact]
    public void EvaluationResult_EmptyResult()
    {
        var result = new EvaluationResult(0, []);
        result.Total.Should().Be(0);
        result.Concepts.Should().BeEmpty();
    }

    #endregion

    #region Settings Defaults

    [Fact]
    public void Settings_EvaluateHotKeyDefaults()
    {
        var settings = new Settings();
        settings.EvaluateHotKeyModifiers.Should().Be(0x0002 | 0x0004, "default should be Ctrl+Shift");
        settings.EvaluateHotKeyVirtualKey.Should().Be(0x56, "default should be 'V'");
    }

    [Fact]
    public void Settings_EvaluateResultLimitDefault()
    {
        var settings = new Settings();
        settings.EvaluateResultLimit.Should().Be(50);
    }

    #endregion

    #region BuildConceptWarnings

    [Fact]
    public void BuildConceptWarnings_AllActive_NoWarnings()
    {
        var ids = new List<string> { "73211009", "404684003" };
        var batch = new BatchLookupResult(
            new() { ["73211009"] = "Diabetes", ["404684003"] = "Clinical finding" },
            new(), new() { ["73211009"] = true, ["404684003"] = true });

        var warnings = TrayAppContext.BuildConceptWarnings(ids, batch);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void BuildConceptWarnings_InactiveConcept_ShowsWarning()
    {
        var ids = new List<string> { "73211009" };
        var batch = new BatchLookupResult(
            new() { ["73211009"] = "Diabetes" },
            new(), new() { ["73211009"] = false });

        var warnings = TrayAppContext.BuildConceptWarnings(ids, batch);
        warnings.Should().ContainSingle().Which.Should().Contain("inactive");
    }

    [Fact]
    public void BuildConceptWarnings_UnknownConcept_ShowsWarning()
    {
        var ids = new List<string> { "99999999" };
        var batch = new BatchLookupResult(new(), new(), new());

        var warnings = TrayAppContext.BuildConceptWarnings(ids, batch);
        warnings.Should().ContainSingle().Which.Should().Contain("not found");
    }

    [Fact]
    public void BuildConceptWarnings_Mixed_ReportsAll()
    {
        var ids = new List<string> { "73211009", "99999999", "404684003" };
        var batch = new BatchLookupResult(
            new() { ["73211009"] = "Diabetes", ["404684003"] = "Finding" },
            new(),
            new() { ["73211009"] = false, ["404684003"] = true });

        var warnings = TrayAppContext.BuildConceptWarnings(ids, batch);
        warnings.Should().HaveCount(2);
        warnings.Should().Contain(w => w.Contains("inactive"));
        warnings.Should().Contain(w => w.Contains("not found"));
    }

    [Fact]
    public void BuildConceptWarnings_EmptyList_NoWarnings()
    {
        var warnings = TrayAppContext.BuildConceptWarnings([], new BatchLookupResult(new(), new(), new()));
        warnings.Should().BeEmpty();
    }

    #endregion
}
