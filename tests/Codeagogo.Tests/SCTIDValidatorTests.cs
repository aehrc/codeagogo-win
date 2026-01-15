// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for SNOMED CT ID validation using the Verhoeff algorithm
/// and concept ID extraction from text.
/// </summary>
public class SCTIDValidatorTests
{
    #region Valid SCTID Tests

    [Fact]
    public void IsValidSCTID_DiabetesMellitus_ReturnsTrue()
    {
        // 73211009 = Diabetes mellitus (disorder)
        SCTIDValidator.IsValidSCTID("73211009").Should().BeTrue();
    }

    [Fact]
    public void IsValidSCTID_ClinicalFinding_ReturnsTrue()
    {
        // 404684003 = Clinical finding (finding)
        SCTIDValidator.IsValidSCTID("404684003").Should().BeTrue();
    }

    [Fact]
    public void IsValidSCTID_InternationalModule_ReturnsTrue()
    {
        // 900000000000207008 = SNOMED CT core module (18 digits)
        SCTIDValidator.IsValidSCTID("900000000000207008").Should().BeTrue();
    }

    [Fact]
    public void IsValidSCTID_Aspirin_ReturnsTrue()
    {
        // 387458008 = Aspirin (substance)
        SCTIDValidator.IsValidSCTID("387458008").Should().BeTrue();
    }

    [Fact]
    public void IsValidSCTID_FSNDesignationCode_ReturnsTrue()
    {
        // 900000000000003001 = Fully Specified Name designation code
        SCTIDValidator.IsValidSCTID("900000000000003001").Should().BeTrue();
    }

    [Theory]
    [InlineData("73211009")]      // Diabetes mellitus
    [InlineData("404684003")]     // Clinical finding
    [InlineData("387458008")]     // Aspirin
    [InlineData("385804009")]     // Diabetic care
    [InlineData("138875005")]     // SNOMED CT root concept
    [InlineData("48176007")]      // Social context
    public void IsValidSCTID_RealSNOMEDCTIDs_ReturnsTrue(string sctid)
    {
        SCTIDValidator.IsValidSCTID(sctid).Should().BeTrue($"Expected {sctid} to be valid");
    }

    #endregion

    #region Invalid Check Digit Tests

    [Fact]
    public void IsValidSCTID_DiabetesWrongCheckDigit_ReturnsFalse()
    {
        // 73211009 with wrong check digit (0 instead of 9)
        SCTIDValidator.IsValidSCTID("73211000").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_InternationalModuleWrongCheckDigit_ReturnsFalse()
    {
        // Wrong check digit
        SCTIDValidator.IsValidSCTID("900000000000207000").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_OffByOne_ReturnsFalse()
    {
        // 73211008 - off by one from valid 73211009
        SCTIDValidator.IsValidSCTID("73211008").Should().BeFalse();
    }

    [Theory]
    [InlineData("73211000")]
    [InlineData("73211001")]
    [InlineData("73211002")]
    public void IsValidSCTID_ModifiedCheckDigit_ReturnsFalse(string invalidSctid)
    {
        SCTIDValidator.IsValidSCTID(invalidSctid).Should().BeFalse();
    }

    #endregion

    #region Length Validation Tests

    [Theory]
    [InlineData("12345")]     // 5 digits - too short
    [InlineData("1234")]      // 4 digits
    [InlineData("1")]         // 1 digit
    public void IsValidSCTID_TooShort_ReturnsFalse(string input)
    {
        SCTIDValidator.IsValidSCTID(input).Should().BeFalse();
    }

    [Theory]
    [InlineData("1234567890123456789")]  // 19 digits
    [InlineData("12345678901234567890")] // 20 digits
    public void IsValidSCTID_TooLong_ReturnsFalse(string input)
    {
        SCTIDValidator.IsValidSCTID(input).Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_Empty_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID("").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_Null_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID(null!).Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_WhitespaceOnly_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID("   ").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_BoundaryLength18Digits_ReturnsTrue()
    {
        // Maximum valid length (18 digits)
        // 900000000000207008 is the SNOMED CT core module ID
        SCTIDValidator.IsValidSCTID("900000000000207008").Should().BeTrue();
    }

    [Fact]
    public void IsValidSCTID_ShortNumeric6Digits_InvalidCheckDigit_ReturnsFalse()
    {
        // A 6-digit number with invalid check digit should fail
        SCTIDValidator.IsValidSCTID("123456").Should().BeFalse();
    }

    #endregion

    #region Non-Numeric Input Tests

    [Fact]
    public void IsValidSCTID_LOINCCode_ReturnsFalse()
    {
        // LOINC code format
        SCTIDValidator.IsValidSCTID("8867-4").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_ICD10Code_ReturnsFalse()
    {
        // ICD-10 code format
        SCTIDValidator.IsValidSCTID("J45.901").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_LettersOnly_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID("ABCDEFGH").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_MixedLettersAndDigits_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID("ABC123456").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_ContainsSpaces_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID("73211 009").Should().BeFalse();
    }

    [Fact]
    public void IsValidSCTID_ContainsSpecialCharacters_ReturnsFalse()
    {
        SCTIDValidator.IsValidSCTID("73211!009").Should().BeFalse();
    }

    #endregion

    #region IsCoreSCTID Tests

    [Theory]
    [InlineData("73211009")]       // Diabetes mellitus - short format, core
    [InlineData("404684003")]      // Clinical finding - short format, core
    [InlineData("138875005")]      // SNOMED CT root - short format, core
    public void IsCoreSCTID_ShortFormatInternational_ReturnsTrue(string sctid)
    {
        SCTIDValidator.IsCoreSCTID(sctid).Should().BeTrue($"{sctid} is a core International Edition SCTID");
    }

    [Theory]
    [InlineData("999000001000168109")]  // Long format, namespaced (Belgian)
    [InlineData("32506021000036107")]   // Australian module ID
    public void IsCoreSCTID_LongFormatNamespaced_ReturnsFalse(string sctid)
    {
        SCTIDValidator.IsCoreSCTID(sctid).Should().BeFalse($"{sctid} is a namespaced (non-core) SCTID");
    }

    [Fact]
    public void IsCoreSCTID_TooShort_ReturnsFalse()
    {
        SCTIDValidator.IsCoreSCTID("12345").Should().BeFalse();
    }

    [Fact]
    public void IsCoreSCTID_ThirdFromEndIsZero_ReturnsTrue()
    {
        // 3rd-from-end digit is 0 => short format
        SCTIDValidator.IsCoreSCTID("123456009").Should().BeTrue();
    }

    [Fact]
    public void IsCoreSCTID_ThirdFromEndIsOne_ReturnsFalse()
    {
        // 3rd-from-end digit is 1 => long format (namespaced)
        SCTIDValidator.IsCoreSCTID("123456119").Should().BeFalse();
    }

    #endregion
}

/// <summary>
/// Tests for SNOMED CT concept ID extraction from text.
/// </summary>
public class ConceptIdExtractionTests
{
    #region ExtractFirstSnomedId Tests

    [Fact]
    public void ExtractFirstSnomedId_SimpleConceptId_ExtractsCorrectly()
    {
        var text = "404684003";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("404684003");
    }

    [Fact]
    public void ExtractFirstSnomedId_WithSurroundingText_ExtractsFirstMatch()
    {
        var text = "The concept 404684003 is for Clinical Finding";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("404684003");
    }

    [Fact]
    public void ExtractFirstSnomedId_WithWhitespace_ExtractsCorrectly()
    {
        var text = "  404684003  ";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("404684003");
    }

    [Fact]
    public void ExtractFirstSnomedId_WithNewlines_ExtractsCorrectly()
    {
        var text = "\n404684003\n";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("404684003");
    }

    [Fact]
    public void ExtractFirstSnomedId_MinimumLength6Digits_ExtractsCorrectly()
    {
        var text = "123456";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("123456");
    }

    [Fact]
    public void ExtractFirstSnomedId_MaximumLength18Digits_ExtractsCorrectly()
    {
        var text = "123456789012345678";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("123456789012345678");
    }

    [Fact]
    public void ExtractFirstSnomedId_MultiplePresent_ExtractsFirst()
    {
        var text = "404684003 and 73211009";
        ClipboardSelectionReader.ExtractFirstSnomedId(text)
            .Should().Be("404684003", "Should extract the first concept ID");
    }

    [Theory]
    [InlineData("404684003")] // Clinical finding
    [InlineData("73211009")]  // Diabetes mellitus
    [InlineData("387458008")] // Aspirin
    [InlineData("900000000000207008")] // SNOMED CT core module
    public void ExtractFirstSnomedId_RealSNOMEDCTIDs_ExtractsCorrectly(string sctid)
    {
        ClipboardSelectionReader.ExtractFirstSnomedId(sctid).Should().Be(sctid);
    }

    [Fact]
    public void ExtractFirstSnomedId_TooShortNumber_ReturnsNull()
    {
        // Less than 6 digits should not match
        ClipboardSelectionReader.ExtractFirstSnomedId("12345").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_TooLongNumber_ReturnsNull()
    {
        // More than 18 digits should not match
        ClipboardSelectionReader.ExtractFirstSnomedId("1234567890123456789").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_EmptyString_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_Null_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId(null).Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_WhitespaceOnly_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("   ").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_TextOnly_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("Hello World").Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_NumberEmbeddedInWord_ReturnsNull()
    {
        // Word boundaries should prevent partial matches
        ClipboardSelectionReader.ExtractFirstSnomedId("ABC123456DEF")
            .Should().BeNull("Should not match digits embedded in words");
    }

    [Fact]
    public void ExtractFirstSnomedId_PipeSeparatedFormat_ExtractsCode()
    {
        // Common copy format: "123456789 | Term | "
        var text = "73211009 | Diabetes mellitus (disorder) | ";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_TabSeparatedFormat_ExtractsCode()
    {
        var text = "73211009\tDiabetes mellitus";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_CommaSeparatedFormat_ExtractsCode()
    {
        var text = "73211009, Diabetes mellitus";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("73211009");
    }

    #endregion

    #region ExtractAllConceptIds Tests

    [Fact]
    public void ExtractAllConceptIds_MultipleCodes_ExtractsAll()
    {
        var text = "385804009 and this other code 999000001000168109";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("385804009");
        matches[0].ExistingTerm.Should().BeNull();
        matches[1].ConceptId.Should().Be("999000001000168109");
        matches[1].ExistingTerm.Should().BeNull();
    }

    [Fact]
    public void ExtractAllConceptIds_PreservesOrder()
    {
        var text = "First: 73211009, Second: 404684003, Third: 387458008";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
        matches[2].ConceptId.Should().Be("387458008");
    }

    [Fact]
    public void ExtractAllConceptIds_WithDuplicates_ExtractsAll()
    {
        var text = "73211009 appears twice: 73211009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_NoMatches_ReturnsEmpty()
    {
        var text = "No concept IDs here, just text";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_SingleCode_ReturnsOne()
    {
        var text = "Just one code: 73211009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().BeNull();
    }

    [Fact]
    public void ExtractAllConceptIds_StartIndexIsCorrect()
    {
        var text = "Code 73211009 is here";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        text.Substring(matches[0].StartIndex, matches[0].Length).Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_Null_ReturnsEmpty()
    {
        ClipboardSelectionReader.ExtractAllConceptIds(null).Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_EmptyString_ReturnsEmpty()
    {
        ClipboardSelectionReader.ExtractAllConceptIds("").Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_WhitespaceOnly_ReturnsEmpty()
    {
        ClipboardSelectionReader.ExtractAllConceptIds("   ").Should().BeEmpty();
    }

    #endregion

    #region Existing Term Detection Tests

    [Fact]
    public void ExtractAllConceptIds_DetectsExistingPipeDelimitedTerm()
    {
        var text = "73211009 | Diabetes mellitus |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_DetectsTermWithExtraWhitespace()
    {
        var text = "73211009  |  Diabetes mellitus  |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_MixedCodesWithAndWithoutTerms()
    {
        var text = "73211009 | Diabetes | and 385804009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().Be("Diabetes");
        matches[1].ConceptId.Should().Be("385804009");
        matches[1].ExistingTerm.Should().BeNull();
    }

    [Fact]
    public void ExtractAllConceptIds_RangeIncludesPipeDelimitedTerm()
    {
        var text = "Code: 73211009 | Diabetes | is here";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        text.Substring(matches[0].StartIndex, matches[0].Length)
            .Should().Be("73211009 | Diabetes |");
    }

    [Fact]
    public void ExtractAllConceptIds_MultipleCodesAllWithTerms()
    {
        var text = "73211009 | Diabetes | and 385804009 | Diabetic care |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ExistingTerm.Should().Be("Diabetes");
        matches[1].ExistingTerm.Should().Be("Diabetic care");
    }

    #endregion

    #region SCTID Validation in ConceptMatch Tests

    [Fact]
    public void ExtractAllConceptIds_ValidSCTID_MarksAsValid()
    {
        // 73211009 is a valid SCTID, 73211000 has invalid check digit
        var text = "Valid: 73211009 and Invalid: 73211000";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].IsSCTID.Should().BeTrue("73211009 should be a valid SCTID");
        matches[1].ConceptId.Should().Be("73211000");
        matches[1].IsSCTID.Should().BeFalse("73211000 should not be a valid SCTID");
    }

    [Fact]
    public void ExtractAllConceptIds_AllValidSCTIDs_MarkedCorrectly()
    {
        var text = "73211009 and 385804009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].IsSCTID.Should().BeTrue();
        matches[1].IsSCTID.Should().BeTrue();
    }

    #endregion

    #region Advanced Extraction Scenarios

    [Fact]
    public void ExtractAllConceptIds_LongModuleId_ExtractsCorrectly()
    {
        // 900000000000207008 = SNOMED CT core module (18 digits)
        var text = "Module 900000000000207008 is the core";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("900000000000207008");
    }

    [Fact]
    public void ExtractAllConceptIds_AdjacentCodes_ExtractsBoth()
    {
        // Two codes separated only by space
        var text = "73211009 404684003";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
    }

    [Fact]
    public void ExtractAllConceptIds_CodesInParentheses_ExtractsBoth()
    {
        var text = "(73211009) and (404684003)";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
    }

    [Fact]
    public void ExtractAllConceptIds_CodesInBrackets_ExtractsBoth()
    {
        var text = "[73211009] and [404684003]";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
    }

    [Fact]
    public void ExtractAllConceptIds_CodesWithColons_ExtractsBoth()
    {
        var text = "Concept: 73211009, Also: 404684003";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
    }

    [Fact]
    public void ExtractAllConceptIds_CodeAtStartOfLine_ExtractsCorrectly()
    {
        var text = "73211009 is at the start";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_CodeAtEndOfLine_ExtractsCorrectly()
    {
        var text = "The code is 73211009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_MultilineText_ExtractsAll()
    {
        var text = "Line 1: 73211009\nLine 2: 404684003\nLine 3: 387458008";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
        matches[2].ConceptId.Should().Be("387458008");
    }

    #endregion

    #region Complex Term Detection Tests

    [Fact]
    public void ExtractAllConceptIds_TermWithParentheses_ExtractsTermCorrectly()
    {
        var text = "73211009 | Diabetes mellitus (disorder) |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus (disorder)");
    }

    [Fact]
    public void ExtractAllConceptIds_TermWithSpecialCharacters_ExtractsTermCorrectly()
    {
        var text = "73211009 | Type 2 diabetes - uncontrolled |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().Be("Type 2 diabetes - uncontrolled");
    }

    [Fact]
    public void ExtractAllConceptIds_TermWithNumbers_ExtractsTermCorrectly()
    {
        var text = "73211009 | Type 2 diabetes mellitus |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().Be("Type 2 diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_TermWithTrailingSpaces_TrimsTerm()
    {
        var text = "73211009 | Diabetes mellitus   |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_TermWithLeadingSpaces_TrimsTerm()
    {
        var text = "73211009 |   Diabetes mellitus |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ExtractAllConceptIds_EmptyPipes_NoTerm()
    {
        var text = "73211009 |  |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
        // Empty term after trimming should either be null or empty
        matches[0].ExistingTerm.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_NoClosingPipe_NoTermDetected()
    {
        // Without closing pipe, it should not be treated as a term
        var text = "73211009 | Diabetes mellitus";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().BeNull();
    }

    #endregion

    #region Multiple Codes with Mixed Formats Tests

    [Fact]
    public void ExtractAllConceptIds_MixedFormats_ExtractsAllCorrectly()
    {
        var text = "73211009 | Diabetes | then 404684003 followed by 387458008 | Aspirin |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().Be("Diabetes");
        matches[1].ConceptId.Should().Be("404684003");
        matches[1].ExistingTerm.Should().BeNull();
        matches[2].ConceptId.Should().Be("387458008");
        matches[2].ExistingTerm.Should().Be("Aspirin");
    }

    [Fact]
    public void ExtractAllConceptIds_BulletListFormat_ExtractsAll()
    {
        var text = @"- 73211009 | Diabetes mellitus |
- 404684003 | Clinical finding |
- 387458008 | Aspirin |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
        matches[1].ExistingTerm.Should().Be("Clinical finding");
        matches[2].ExistingTerm.Should().Be("Aspirin");
    }

    [Fact]
    public void ExtractAllConceptIds_NumberedListFormat_ExtractsAll()
    {
        var text = @"1. 73211009
2. 404684003
3. 387458008";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
        matches[2].ConceptId.Should().Be("387458008");
    }

    [Fact]
    public void ExtractAllConceptIds_CsvFormat_ExtractsAll()
    {
        var text = "73211009,404684003,387458008";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
        matches[2].ConceptId.Should().Be("387458008");
    }

    [Fact]
    public void ExtractAllConceptIds_TabDelimitedFormat_ExtractsAll()
    {
        var text = "73211009\t404684003\t387458008";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("404684003");
        matches[2].ConceptId.Should().Be("387458008");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ExtractFirstSnomedId_NumberEmbeddedAfterLetters_ReturnsNull()
    {
        // Word boundary should prevent match when digits immediately follow letters
        ClipboardSelectionReader.ExtractFirstSnomedId("CODE73211009")
            .Should().BeNull("Digits immediately following letters should not match");
    }

    [Fact]
    public void ExtractFirstSnomedId_NumberEmbeddedBeforeLetters_ReturnsNull()
    {
        // Word boundary should prevent match when digits immediately precede letters
        ClipboardSelectionReader.ExtractFirstSnomedId("73211009ABC")
            .Should().BeNull("Digits immediately preceding letters should not match");
    }

    [Fact]
    public void ExtractFirstSnomedId_NumberWithHyphenInMiddle_ExtractsFirstPart()
    {
        // Hyphens break word boundaries, so 73211009 should be extracted
        var result = ClipboardSelectionReader.ExtractFirstSnomedId("73211009-404684003");
        result.Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_LeadingZeros_ExtractsCorrectly()
    {
        var text = "000073211009";
        // Should extract the full 12-digit number including leading zeros
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("000073211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_OnlyZeros_ExtractsIfValidLength()
    {
        var text = "000000";
        ClipboardSelectionReader.ExtractFirstSnomedId(text).Should().Be("000000");
    }

    [Fact]
    public void ExtractAllConceptIds_VeryLongText_ExtractsAllEfficiently()
    {
        // Build a long text with many codes
        var codes = new[] { "73211009", "404684003", "387458008", "385804009", "138875005" };
        var text = string.Join(" some text between codes ", codes);
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(5);
        for (int i = 0; i < codes.Length; i++)
        {
            matches[i].ConceptId.Should().Be(codes[i]);
        }
    }

    [Fact]
    public void ExtractAllConceptIds_UnicodeText_ExtractsCodesCorrectly()
    {
        var text = "Diagnose: 73211009 | Diabetes \u00e7\u00e9\u00e0 |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_ExactlyBoundaryLength_ExtractsCorrectly()
    {
        // 6 digits (minimum)
        var text6 = "123456";
        ClipboardSelectionReader.ExtractAllConceptIds(text6).Should().HaveCount(1);

        // 18 digits (maximum)
        var text18 = "123456789012345678";
        ClipboardSelectionReader.ExtractAllConceptIds(text18).Should().HaveCount(1);
    }

    [Fact]
    public void ExtractAllConceptIds_JustBelowMinimum_ReturnsEmpty()
    {
        // 5 digits - too short
        var text = "12345";
        ClipboardSelectionReader.ExtractAllConceptIds(text).Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_JustAboveMaximum_ExtractsFirst18Digits()
    {
        // 19 digits - regex extracts first 18 digits greedily
        var text = "1234567890123456789";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);
        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("123456789012345678");
    }

    [Fact]
    public void ExtractAllConceptIds_MixedValidAndInvalidLengths_ExtractsAllValidLengths()
    {
        // 5 digits is too short, but both 8-digit and 18-digit sequences are valid lengths
        var text = "12345 then 73211009 then 1234567890123456789";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[1].ConceptId.Should().Be("123456789012345678"); // first 18 digits extracted
    }

    [Fact]
    public void ExtractAllConceptIds_ConsecutiveDigitsExceedingMax_ExtractsFirst18()
    {
        // 20 consecutive digits - regex extracts first 18 digits
        var text = "12345678901234567890";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);
        matches.Should().HaveCount(1);
        matches[0].ConceptId.Should().Be("123456789012345678");
    }

    #endregion

    #region SCTID Validation Edge Cases Tests

    [Fact]
    public void ExtractAllConceptIds_ValidAndInvalidSCTIDsMixed_MarksCorrectly()
    {
        // Mix of valid SCTIDs and numbers with invalid check digits
        var text = "73211009 and 73211001 and 404684003 and 404684000";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(4);
        matches[0].IsSCTID.Should().BeTrue("73211009 is valid");
        matches[1].IsSCTID.Should().BeFalse("73211001 has invalid check digit");
        matches[2].IsSCTID.Should().BeTrue("404684003 is valid");
        matches[3].IsSCTID.Should().BeFalse("404684000 has invalid check digit");
    }

    [Fact]
    public void ExtractAllConceptIds_AllInvalidCheckDigits_AllMarkedFalse()
    {
        var text = "73211000 and 73211001 and 73211002";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches.Should().AllSatisfy(m => m.IsSCTID.Should().BeFalse());
    }

    [Fact]
    public void ExtractAllConceptIds_ShortCodesWithValidChecksum_ValidatedCorrectly()
    {
        // Test with minimum-length valid SCTIDs
        var text = "48176007"; // Social context - valid SCTID
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(1);
        matches[0].IsSCTID.Should().BeTrue();
    }

    [Fact]
    public void ExtractAllConceptIds_LongModuleIds_ValidatedCorrectly()
    {
        // 18-digit module IDs
        var text = "900000000000207008 and 900000000000003001";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].IsSCTID.Should().BeTrue("900000000000207008 is valid SNOMED CT core module");
        matches[1].IsSCTID.Should().BeTrue("900000000000003001 is valid FSN designation code");
    }

    #endregion

    #region Alphanumeric Code Extraction Tests (LOINC, ICD-10)

    [Fact]
    public void ExtractAllConceptIds_LOINCCode_ExtractsCorrectly()
    {
        var text = "The LOINC code 8867-4 represents heart rate";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().ContainSingle(m => m.ConceptId == "8867-4");
    }

    [Fact]
    public void ExtractAllConceptIds_ICD10Code_ExtractsCorrectly()
    {
        var text = "ICD-10 code E11.9 for Type 2 diabetes";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().ContainSingle(m => m.ConceptId == "E11.9");
    }

    [Fact]
    public void ExtractAllConceptIds_MixedSNOMEDAndLOINC_ExtractsBoth()
    {
        var text = "SNOMED 73211009 and LOINC 8867-4";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].IsSCTID.Should().BeTrue();
        matches[1].ConceptId.Should().Be("8867-4");
        matches[1].IsSCTID.Should().BeFalse();
    }

    [Fact]
    public void ExtractAllConceptIds_MixedSNOMEDAndICD10_ExtractsBoth()
    {
        var text = "73211009 |Diabetes mellitus| maps to E11.9";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(2);
        matches[0].ConceptId.Should().Be("73211009");
        matches[0].ExistingTerm.Should().Be("Diabetes mellitus");
        matches[1].ConceptId.Should().Be("E11.9");
    }

    [Fact]
    public void ExtractAllConceptIds_AlphanumericCodeWithPipeTerm_ExtractsTerm()
    {
        var text = "8867-4 |Heart rate|";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().ContainSingle();
        matches[0].ConceptId.Should().Be("8867-4");
        matches[0].ExistingTerm.Should().Be("Heart rate");
    }

    [Fact]
    public void ExtractAllConceptIds_PureText_NotMatchedByAlphanumericPass()
    {
        var text = "Hello world no codes here";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().BeEmpty();
    }

    [Fact]
    public void ExtractAllConceptIds_ECLDotDot_NotMatchedAsCode()
    {
        // "0..0" in ECL cardinality syntax should not be matched as a code
        var text = "[0..0] 73211009 |Diabetes|";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        // Should only match the SNOMED code, not ECL syntax
        matches.Should().ContainSingle(m => m.ConceptId == "73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_ICD10WithSubcategory_ExtractsCorrectly()
    {
        var text = "J45.901 is unspecified asthma";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().ContainSingle(m => m.ConceptId == "J45.901");
    }

    [Fact]
    public void ExtractAllConceptIds_AlphanumericCodesAreSortedByPosition()
    {
        var text = "E11.9 then 73211009 then 8867-4";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        matches.Should().HaveCount(3);
        matches[0].ConceptId.Should().Be("E11.9");
        matches[1].ConceptId.Should().Be("73211009");
        matches[2].ConceptId.Should().Be("8867-4");
    }

    #endregion

    #region Text Replacement Simulation Tests

    [Fact]
    public void ExtractAllConceptIds_ReplacementPreservesText()
    {
        var text = "385804009 and this other code 999000001000168109";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        // Simulate replacement (reverse order to preserve indices)
        var result = text;
        foreach (var match in matches.OrderByDescending(m => m.StartIndex))
        {
            var replacement = $"{match.ConceptId} | Term |";
            result = result.Remove(match.StartIndex, match.Length).Insert(match.StartIndex, replacement);
        }

        result.Should().Be("385804009 | Term | and this other code 999000001000168109 | Term |");
    }

    [Fact]
    public void ExtractAllConceptIds_RemoveTermsSimulation()
    {
        // Simulate the "remove" toggle behavior
        var text = "73211009 | Diabetes | and 385804009 | Diabetic care |";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        // Replace in reverse order with just the concept IDs
        var result = text;
        foreach (var match in matches.OrderByDescending(m => m.StartIndex))
        {
            result = result.Remove(match.StartIndex, match.Length).Insert(match.StartIndex, match.ConceptId);
        }

        result.Should().Be("73211009 and 385804009");
    }

    [Fact]
    public void ExtractAllConceptIds_UpdateTermsSimulation()
    {
        // Simulate updating wrong terms to correct ones
        var text = "73211009 | Wrong term | and 385804009";
        var matches = ClipboardSelectionReader.ExtractAllConceptIds(text);

        // Replace with "correct" terms
        var result = text;
        foreach (var match in matches.OrderByDescending(m => m.StartIndex))
        {
            var newTerm = match.ConceptId == "73211009" ? "Diabetes mellitus" : "Diabetic care";
            var replacement = $"{match.ConceptId} | {newTerm} |";
            result = result.Remove(match.StartIndex, match.Length).Insert(match.StartIndex, replacement);
        }

        result.Should().Be("73211009 | Diabetes mellitus | and 385804009 | Diabetic care |");
    }

    #endregion
}
