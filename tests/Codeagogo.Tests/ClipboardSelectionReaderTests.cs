// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for ClipboardSelectionReader static extraction methods.
/// These tests cover ExtractFirstSnomedId and ExtractAllConceptIds
/// beyond what is already tested in SCTIDValidatorTests.cs.
/// </summary>
public class ClipboardSelectionReaderTests
{
    #region ExtractFirstSnomedId - Valid IDs

    [Theory]
    [InlineData("123456", "123456")]           // 6 digits - minimum
    [InlineData("1234567", "1234567")]         // 7 digits
    [InlineData("12345678", "12345678")]       // 8 digits
    [InlineData("123456789", "123456789")]     // 9 digits
    [InlineData("123456789012345678", "123456789012345678")] // 18 digits - maximum
    public void ExtractFirstSnomedId_ValidLengths_Extracts(string input, string expected)
    {
        ClipboardSelectionReader.ExtractFirstSnomedId(input).Should().Be(expected);
    }

    #endregion

    #region ExtractFirstSnomedId - Text Before/After

    [Fact]
    public void ExtractFirstSnomedId_TextBefore_ExtractsId()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("The concept 73211009 is here")
            .Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_TextAfter_ExtractsId()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("73211009 is a SNOMED concept")
            .Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_TextBeforeAndAfter_ExtractsId()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("Look at 73211009 in the system")
            .Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_MultipleIds_ExtractsFirst()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("73211009 and 404684003")
            .Should().Be("73211009");
    }

    #endregion

    #region ExtractFirstSnomedId - Null/Empty

    [Fact]
    public void ExtractFirstSnomedId_Null_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId(null).Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_Empty_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_Whitespace_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("   \t\n  ").Should().BeNull();
    }

    #endregion

    #region ExtractFirstSnomedId - Too Short

    [Fact]
    public void ExtractFirstSnomedId_FiveDigits_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("12345").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_FourDigits_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("1234").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_SingleDigit_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("5").Should().BeNull();
    }

    #endregion

    #region ExtractAllConceptIds - Single Code

    [Fact]
    public void ExtractAllConceptIds_SingleCode_ReturnsOneMatch()
    {
        var matches = ClipboardSelectionReader.ExtractAllConceptIds("73211009");

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
    }

    #endregion

    #region ExtractAllConceptIds - Multiple Codes

    [Fact]
    public void ExtractAllConceptIds_TwoCodes_ReturnsBothInOrder()
    {
        var matches = ClipboardSelectionReader.ExtractAllConceptIds("73211009 and 404684003");

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
    }

    [Fact]
    public void ExtractAllConceptIds_ManyCodes_ReturnsAll()
    {
        var text = "73211009, 404684003, 387458008, 385804009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(4);
    }

    #endregion

    #region ExtractAllConceptIds - Code + Pipe-Delimited Term

    [Fact]
    public void ExtractAllConceptIds_CodeWithPipeTerm_ExtractsTermCorrectly()
    {
        var text = "73211009 |Diabetes mellitus|";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_CodeWithPipeTermAndSpaces_TrimsWhitespace()
    {
        var text = "73211009  |  Diabetes mellitus  |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_CodeWithoutPipeTerm_NoTerm()
    {
        var text = "73211009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().BeNull();
    }

    #endregion

    #region ExtractAllConceptIds - SCTID Validation

    [Fact]
    public void ExtractAllConceptIds_ValidSctid_IsSctidTrue()
    {
        var matches = ClipboardSelectionReader.ExtractAllConceptIds("73211009");

        matches.Should().HaveCount(1);
        matches[0].IsSCTID.Should().BeTrue("73211009 is a valid SCTID");
    }

    [Fact]
    public void ExtractAllConceptIds_InvalidCheckDigit_IsSctidFalse()
    {
        var matches = ClipboardSelectionReader.ExtractAllConceptIds("73211000");

        matches.Should().HaveCount(1);
        matches[0].IsSCTID.Should().BeFalse("73211000 has an invalid check digit");
    }

    [Fact]
    public void ExtractAllConceptIds_MixedValidity_ReportsCorrectly()
    {
        var matches = ClipboardSelectionReader.ExtractAllConceptIds("73211009 and 73211000");

        matches.Should().HaveCount(2);
        matches[0].IsSCTID.Should().BeTrue();
        matches[1].IsSCTID.Should().BeFalse();
    }

    #endregion

    #region ExtractAllConceptIds - Null/Empty

    [Fact]
    public void ExtractAllConceptIds_NullInput_ReturnsEmptyList()
    {
        ClipboardSelectionReader.ExtractAllConceptIds(null).Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_EmptyString_ReturnsEmptyList()
    {
        ClipboardSelectionReader.ExtractAllConceptIds("").Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_WhitespaceOnly_ReturnsEmptyList()
    {
        ClipboardSelectionReader.ExtractAllConceptIds("   ").Should().BeEmpty();
    }

    #endregion

    #region ConceptMatch Positions and Lengths

    [Fact]
    public void ExtractAllConceptIds_StartIndex_IsCorrect()
    {
        var text = "prefix 73211009 suffix";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].StartIndex.Should().Be(7); // "prefix " = 7 chars
    }

    [Fact]
    public void ExtractAllConceptIds_LengthWithoutTerm_IsIdLength()
    {
        var text = "73211009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].Length.Should().Be(8); // "73211009" = 8 chars
    }

    [Fact]
    public void ExtractAllConceptIds_LengthWithPipeTerm_IncludesFullMatch()
    {
        var text = "73211009 |Diabetes mellitus|";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        // Full match includes the pipe term
        var matchedText = text.Substring(matches[0].StartIndex, matches[0].Length);
        matchedText.Should().Contain("73211009");
        matchedText.Should().Contain("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_MultipleMatchPositions_AreCorrect()
    {
        var text = "A: 73211009, B: 404684003";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        text.Substring(matches[0].StartIndex, matches[0].Length).Should().Contain("73211009");
        text.Substring(matches[1].StartIndex, matches[1].Length).Should().Contain("404684003");
    }

    #endregion

    #region ConceptMatch Record Equality

    [Fact]
    public void ConceptMatch_SameValues_AreEqual()
    {
        var a = new ConceptMatch("73211009", 0, 8, null, true);
        var b = new ConceptMatch("73211009", 0, 8, null, true);

        a.Should().Be(b);
    }

    [Fact]
    public void ConceptMatch_DifferentValues_AreNotEqual()
    {
        var a = new ConceptMatch("73211009", 0, 8, null, true);
        var b = new ConceptMatch("404684003", 0, 9, null, true);

        a.Should().NotBe(b);
    }

    #endregion
}
