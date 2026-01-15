// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for the Jint-based ECL bridge to ecl-core.
/// These tests verify that the ecl-core TypeScript library runs correctly
/// inside Jint, providing parsing, formatting, validation, and concept extraction.
/// </summary>
public class ECLBridgeTests
{
    private static readonly Lazy<ECLBridge> SharedBridge = new(() =>
    {
        // Load from the main project's embedded resource by loading the bundle file directly
        var projectDir = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Codeagogo", "ecl-core-bundle.js"));
        var js = File.ReadAllText(projectDir);
        return new ECLBridge(js);
    });

    private ECLBridge Bridge => SharedBridge.Value;

    #region Bundle Loading

    [Fact]
    public void Bridge_LoadsSuccessfully()
    {
        Bridge.IsLoaded.Should().BeTrue();
    }

    #endregion

    #region Formatting Tests

    [Fact]
    public void FormatECL_SimpleExpression_ReturnsFormatted()
    {
        var result = Bridge.FormatECL("<< 404684003");
        result.Should().NotBeNull();
        result!.Trim().Should().Be("<< 404684003");
    }

    [Fact]
    public void FormatECL_ExpressionWithTerm_PreservesTerm()
    {
        var result = Bridge.FormatECL("<<404684003|Clinical finding|");
        result.Should().NotBeNull();
        result.Should().Contain("Clinical finding");
    }

    [Fact]
    public void FormatECL_RefinedExpression_ContainsColon()
    {
        var result = Bridge.FormatECL("<< 404684003: 363698007 = << 39057004");
        result.Should().NotBeNull();
        result.Should().Contain(":");
    }

    [Fact]
    public void FormatECL_Wildcard_ReturnsWildcard()
    {
        var result = Bridge.FormatECL("*");
        result!.Trim().Should().Be("*");
    }

    [Fact]
    public void FormatECL_MemberOf_ContainsCaret()
    {
        var result = Bridge.FormatECL("^ 700043003");
        result.Should().NotBeNull();
        result.Should().Contain("^");
    }

    [Fact]
    public void FormatECL_CompoundAND_ContainsAND()
    {
        var result = Bridge.FormatECL("<< 73211009 AND << 404684003");
        result.Should().NotBeNull();
        result.Should().Contain("AND");
    }

    [Fact]
    public void FormatECL_CompoundOR_ContainsOR()
    {
        var result = Bridge.FormatECL("<< 73211009 OR << 404684003");
        result.Should().NotBeNull();
        result.Should().Contain("OR");
    }

    [Fact]
    public void FormatECL_MINUS_ContainsMINUS()
    {
        var result = Bridge.FormatECL("<< 73211009 MINUS << 404684003");
        result.Should().NotBeNull();
        result.Should().Contain("MINUS");
    }

    [Fact]
    public void FormatECL_Cardinality_PreservesCardinality()
    {
        var result = Bridge.FormatECL("<< 404684003: [1..3] 363698007 = << 39057004");
        result.Should().NotBeNull();
        result.Should().Contain("[1..3]");
    }

    [Fact]
    public void FormatECL_InvalidECL_DoesNotCrash()
    {
        // formatDocument returns best-effort output for invalid ECL
        var act = () => Bridge.FormatECL("<<<>>>");
        act.Should().NotThrow();
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void ParseECL_ValidExpression_HasAST()
    {
        var result = Bridge.ParseECL("<< 404684003 |Clinical finding|");
        result.HasAST.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ParseECL_CompoundExpression_HasAST()
    {
        var result = Bridge.ParseECL("<< 73211009 OR << 404684003");
        result.HasAST.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ParseECL_InvalidExpression_HasErrors()
    {
        var result = Bridge.ParseECL("<< AND OR");
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseECL_EmptyString_DoesNotCrash()
    {
        var act = () => Bridge.ParseECL("");
        act.Should().NotThrow();
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData("404684003", true)]
    [InlineData("73211009", true)]
    [InlineData("123456789", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void IsValidConceptId_ReturnsExpected(string sctid, bool expected)
    {
        Bridge.IsValidConceptId(sctid).Should().Be(expected);
    }

    [Theory]
    [InlineData("<< 404684003", true)]
    [InlineData("< 73211009 OR < 404684003", true)]
    [InlineData("<<<>>>", false)]
    public void IsValidECL_ReturnsExpected(string ecl, bool expected)
    {
        Bridge.IsValidECL(ecl).Should().Be(expected);
    }

    #endregion

    #region Concept Extraction Tests

    [Fact]
    public void ExtractConceptIds_RefinedExpression_ExtractsAllIds()
    {
        var concepts = Bridge.ExtractConceptIds(
            "<< 404684003 |Clinical finding|: 363698007 = << 39057004");
        var ids = concepts.Select(c => c.Id).ToList();
        ids.Should().Contain("404684003");
        ids.Should().Contain("363698007");
        ids.Should().Contain("39057004");
    }

    [Fact]
    public void ExtractConceptIds_WithTerms_ExtractsTerm()
    {
        var concepts = Bridge.ExtractConceptIds("<< 404684003 |Clinical finding|");
        concepts.Should().NotBeEmpty();
        var clinical = concepts.FirstOrDefault(c => c.Id == "404684003");
        clinical.Should().NotBeNull();
        clinical!.Term.Should().Be("Clinical finding");
    }

    [Fact]
    public void ExtractConceptIds_InvalidECL_ReturnsEmpty()
    {
        var concepts = Bridge.ExtractConceptIds("<<<>>>");
        concepts.Should().BeEmpty();
    }

    #endregion

    #region Toggle Tests

    [Fact]
    public void ToggleECLFormat_MinifiedToFormatted_ReturnsNonNull()
    {
        var input = "<< 404684003 |Clinical finding|: 363698007 |Finding site| = << 39057004 |Pulmonary valve structure|, 116676008 |Associated morphology| = << 415582006 |Stenosis|";
        var result = Bridge.ToggleECLFormat(input);
        result.Should().NotBeNull();
    }

    [Fact]
    public void ToggleECLFormat_RoundTrip_ReturnsNonEmpty()
    {
        var input = "<< 404684003";
        var first = Bridge.ToggleECLFormat(input);
        first.Should().NotBeNull();
        var second = Bridge.ToggleECLFormat(first!);
        second.Should().NotBeNull();
        second.Should().NotBeEmpty();
    }

    [Fact]
    public void ToggleECLFormat_InvalidECL_ReturnsNonNull()
    {
        // ecl-core's formatDocument returns best-effort output for invalid ECL
        var result = Bridge.ToggleECLFormat("<<<>>>");
        result.Should().NotBeNull();
    }

    #endregion

    #region Knowledge Base Tests

    [Fact]
    public void GetArticles_ReturnsArticles()
    {
        var articles = Bridge.GetArticles();
        articles.Should().NotBeEmpty();
        articles.Should().HaveCountGreaterThanOrEqualTo(40,
            "Knowledge base should contain at least 40 articles");
    }

    [Fact]
    public void GetArticles_ArticlesHaveRequiredFields()
    {
        var articles = Bridge.GetArticles();
        articles.Should().NotBeEmpty();

        var first = articles[0];
        first.Id.Should().NotBeNullOrEmpty();
        first.Name.Should().NotBeNullOrEmpty();
        first.Summary.Should().NotBeNullOrEmpty();
        first.Category.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetArticles_CoversAllCategories()
    {
        var articles = Bridge.GetArticles();
        var categories = articles.Select(a => a.Category).Distinct().ToList();

        categories.Should().Contain("operator");
        categories.Should().Contain("refinement");
        categories.Should().Contain("filter");
        categories.Should().Contain("pattern");
        categories.Should().Contain("grammar");
        categories.Should().Contain("history");
    }

    [Fact]
    public void GetArticles_MostHaveContent()
    {
        var articles = Bridge.GetArticles();
        var withContent = articles.Count(a => !string.IsNullOrEmpty(a.Content));
        withContent.Should().BeGreaterThan(40);
    }

    [Fact]
    public void GetOperatorDocs_ReturnsOperatorDocs()
    {
        var docs = Bridge.GetOperatorDocs();
        docs.Should().NotBeEmpty();
        docs.Should().HaveCountGreaterThanOrEqualTo(10);
    }

    #endregion
}
