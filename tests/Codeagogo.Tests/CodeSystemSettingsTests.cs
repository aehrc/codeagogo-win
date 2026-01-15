// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for CodeSystemSettings, ConfiguredCodeSystem, and ConceptResult code system handling.
/// Translated from Swift/Mac CodeSystemSettingsTests.swift.
/// </summary>
public class CodeSystemSettingsTests
{
    #region ConfiguredCodeSystem Tests

    [Fact]
    public void ConfiguredCodeSystem_Initialization_SetsProperties()
    {
        // Arrange & Act
        var system = new ConfiguredCodeSystem(
            Uri: "http://loinc.org",
            Title: "LOINC",
            Enabled: true
        );

        // Assert
        system.Uri.Should().Be("http://loinc.org");
        system.Title.Should().Be("LOINC");
        system.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ConfiguredCodeSystem_DisabledSystem_EnabledIsFalse()
    {
        // Arrange & Act
        var system = new ConfiguredCodeSystem(
            Uri: "http://loinc.org",
            Title: "LOINC",
            Enabled: false
        );

        // Assert
        system.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ConfiguredCodeSystem_Equality_SameValuesAreEqual()
    {
        // Arrange
        var system1 = new ConfiguredCodeSystem(Uri: "http://loinc.org", Title: "LOINC", Enabled: true);
        var system2 = new ConfiguredCodeSystem(Uri: "http://loinc.org", Title: "LOINC", Enabled: true);
        var system3 = new ConfiguredCodeSystem(Uri: "http://rxnorm.org", Title: "RxNorm", Enabled: true);

        // Assert
        system1.Should().Be(system2);
        system1.Should().NotBe(system3);
    }

    [Fact]
    public void ConfiguredCodeSystem_HashCode_SameValuesHaveSameHash()
    {
        // Arrange
        var system1 = new ConfiguredCodeSystem(Uri: "http://loinc.org", Title: "LOINC", Enabled: true);
        var system2 = new ConfiguredCodeSystem(Uri: "http://loinc.org", Title: "LOINC", Enabled: true);

        // Assert
        system1.GetHashCode().Should().Be(system2.GetHashCode());
    }

    [Fact]
    public void ConfiguredCodeSystem_JsonSerialization_RoundTrips()
    {
        // Arrange
        var original = new ConfiguredCodeSystem(
            Uri: "http://loinc.org",
            Title: "LOINC",
            Enabled: false
        );

        // Act
        var json = JsonSerializer.Serialize(original);
        var decoded = JsonSerializer.Deserialize<ConfiguredCodeSystem>(json);

        // Assert
        decoded.Should().NotBeNull();
        decoded!.Uri.Should().Be(original.Uri);
        decoded.Title.Should().Be(original.Title);
        decoded.Enabled.Should().Be(original.Enabled);
    }

    [Fact]
    public void ConfiguredCodeSystem_JsonSerialization_ProducesExpectedJson()
    {
        // Arrange
        var system = new ConfiguredCodeSystem(
            Uri: "http://snomed.info/sct",
            Title: "SNOMED CT",
            Enabled: true
        );

        // Act
        var json = JsonSerializer.Serialize(system);

        // Assert
        json.Should().Contain("Uri");
        json.Should().Contain("http://snomed.info/sct");
        json.Should().Contain("Title");
        json.Should().Contain("SNOMED CT");
        json.Should().Contain("Enabled");
        json.Should().Contain("true");
    }

    #endregion

    #region ConceptResult SystemName Tests

    [Fact]
    public void ConceptResult_SystemName_ForSNOMED_ReturnsSNOMEDCT()
    {
        // Arrange - null system defaults to SNOMED CT
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Diabetes mellitus (disorder)",
            Pt: "Diabetes mellitus",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: null
        );

        // Assert
        result.SystemName.Should().Be("SNOMED CT");
        result.IsSnomedCT.Should().BeTrue();
    }

    [Fact]
    public void ConceptResult_SystemName_ForSNOMEDExplicit_ReturnsSNOMEDCT()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Diabetes mellitus (disorder)",
            Pt: "Diabetes mellitus",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://snomed.info/sct"
        );

        // Assert
        result.SystemName.Should().Be("SNOMED CT");
        result.IsSnomedCT.Should().BeTrue();
    }

    [Fact]
    public void ConceptResult_SystemName_ForSNOMEDExtension_ReturnsSNOMEDCT()
    {
        // Arrange - xsct is used for SNOMED CT extensions
        var result = new ConceptResult(
            ConceptId: "32570271000036106",
            Branch: "Australia",
            Fsn: "Australian English (foundation metadata concept)",
            Pt: "Australian English",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://snomed.info/xsct"
        );

        // Assert
        result.SystemName.Should().Be("SNOMED CT");
        result.IsSnomedCT.Should().BeTrue();
    }

    [Fact]
    public void ConceptResult_SystemName_ForLOINC_ReturnsLOINC()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "8867-4",
            Branch: "",
            Fsn: null,
            Pt: "Heart rate",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://loinc.org"
        );

        // Assert
        result.SystemName.Should().Be("LOINC");
        result.IsSnomedCT.Should().BeFalse();
    }

    [Fact]
    public void ConceptResult_SystemName_ForRxNorm_ReturnsRxNorm()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "1049502",
            Branch: "",
            Fsn: null,
            Pt: "Aspirin 81 MG Oral Tablet",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://www.nlm.nih.gov/research/umls/rxnorm"
        );

        // Assert
        result.SystemName.Should().Be("RxNorm");
        result.IsSnomedCT.Should().BeFalse();
    }

    [Fact]
    public void ConceptResult_SystemName_ForICD10CM_ReturnsICD10CM()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "J45.901",
            Branch: "",
            Fsn: null,
            Pt: "Unspecified asthma, uncomplicated",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://hl7.org/fhir/sid/icd-10-cm"
        );

        // Assert
        result.SystemName.Should().Be("ICD-10-CM");
        result.IsSnomedCT.Should().BeFalse();
    }

    [Fact]
    public void ConceptResult_SystemName_ForICD10_ReturnsICD10()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "E11.9",
            Branch: "",
            Fsn: null,
            Pt: "Type 2 diabetes mellitus without complications",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://hl7.org/fhir/sid/icd-10"
        );

        // Assert
        result.SystemName.Should().Be("ICD-10");
        result.IsSnomedCT.Should().BeFalse();
    }

    [Fact]
    public void ConceptResult_SystemName_ForICD9CM_ReturnsICD9CM()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "250.00",
            Branch: "",
            Fsn: null,
            Pt: "Diabetes mellitus without mention of complication",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://hl7.org/fhir/sid/icd-9-cm"
        );

        // Assert
        result.SystemName.Should().Be("ICD-9-CM");
        result.IsSnomedCT.Should().BeFalse();
    }

    [Fact]
    public void ConceptResult_SystemName_ForUnknown_ReturnsLastPathSegment()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "12345",
            Branch: "",
            Fsn: null,
            Pt: "Test",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://example.org/custom-system"
        );

        // Assert
        result.SystemName.Should().Be("custom-system");
        result.IsSnomedCT.Should().BeFalse();
    }

    [Fact]
    public void ConceptResult_SystemName_ForUnknownWithMultiplePaths_ReturnsLastSegment()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "12345",
            Branch: "",
            Fsn: null,
            Pt: "Test",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://example.org/fhir/sid/my-code-system"
        );

        // Assert
        result.SystemName.Should().Be("my-code-system");
        result.IsSnomedCT.Should().BeFalse();
    }

    #endregion

    #region ConceptResult IsSnomedCT Tests

    [Fact]
    public void ConceptResult_IsSnomedCT_NullSystem_ReturnsTrue()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: null,
            ModuleId: null
        );

        // Assert
        result.System.Should().BeNull();
        result.IsSnomedCT.Should().BeTrue();
    }

    [Fact]
    public void ConceptResult_IsSnomedCT_ExplicitSnomedUri_ReturnsTrue()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://snomed.info/sct"
        );

        // Assert
        result.IsSnomedCT.Should().BeTrue();
    }

    [Fact]
    public void ConceptResult_IsSnomedCT_ExtensionUri_ReturnsTrue()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "32570271000036106",
            Branch: "Australia",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://snomed.info/xsct"
        );

        // Assert
        result.IsSnomedCT.Should().BeTrue();
    }

    [Fact]
    public void ConceptResult_IsSnomedCT_NonSnomedUri_ReturnsFalse()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "8867-4",
            Branch: "",
            Fsn: null,
            Pt: "Heart rate",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://loinc.org"
        );

        // Assert
        result.IsSnomedCT.Should().BeFalse();
    }

    #endregion

    #region ConceptResult ActiveText Tests

    [Fact]
    public void ConceptResult_ActiveText_WhenActive_ReturnsActive()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Test",
            Pt: "Test",
            Active: true,
            EffectiveTime: null,
            ModuleId: null
        );

        // Assert
        result.ActiveText.Should().Be("active");
    }

    [Fact]
    public void ConceptResult_ActiveText_WhenInactive_ReturnsInactive()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Test",
            Pt: "Test",
            Active: false,
            EffectiveTime: null,
            ModuleId: null
        );

        // Assert
        result.ActiveText.Should().Be("inactive");
    }

    [Fact]
    public void ConceptResult_ActiveText_WhenNull_ReturnsDash()
    {
        // Arrange
        var result = new ConceptResult(
            ConceptId: "8867-4",
            Branch: "",
            Fsn: null,
            Pt: "Heart rate",
            Active: null,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://loinc.org"
        );

        // Assert
        result.ActiveText.Should().Be("-");
    }

    #endregion

    #region CodeSystemSettings Tests

    [Fact]
    public void CodeSystemSettings_DefaultSystems_ContainsExpectedSystems()
    {
        // Arrange & Act
        var settings = new CodeSystemSettings();

        // Assert
        settings.Systems.Should().HaveCount(4);
        settings.Systems.Should().Contain(s => s.Uri == "http://snomed.info/sct");
        settings.Systems.Should().Contain(s => s.Uri == "http://loinc.org");
        settings.Systems.Should().Contain(s => s.Uri == "http://hl7.org/fhir/sid/icd-10");
        settings.Systems.Should().Contain(s => s.Uri == "http://www.nlm.nih.gov/research/umls/rxnorm");
    }

    [Fact]
    public void CodeSystemSettings_DefaultSystems_OnlySNOMEDEnabled()
    {
        // Arrange & Act
        var settings = new CodeSystemSettings();

        // Assert
        var snomedSystem = settings.Systems.First(s => s.Uri == "http://snomed.info/sct");
        snomedSystem.Enabled.Should().BeTrue();

        var otherSystems = settings.Systems.Where(s => s.Uri != "http://snomed.info/sct");
        otherSystems.Should().AllSatisfy(s => s.Enabled.Should().BeFalse());
    }

    [Fact]
    public void CodeSystemSettings_EnabledSystemUris_ReturnsOnlyEnabled()
    {
        // Arrange
        var settings = new CodeSystemSettings
        {
            Systems = new List<ConfiguredCodeSystem>
            {
                new("http://snomed.info/sct", "SNOMED CT", true),
                new("http://loinc.org", "LOINC", true),
                new("http://hl7.org/fhir/sid/icd-10", "ICD-10", false),
                new("http://www.nlm.nih.gov/research/umls/rxnorm", "RxNorm", false)
            }
        };

        // Act
        var enabledUris = settings.EnabledSystemUris.ToList();

        // Assert
        enabledUris.Should().HaveCount(2);
        enabledUris.Should().Contain("http://snomed.info/sct");
        enabledUris.Should().Contain("http://loinc.org");
        enabledUris.Should().NotContain("http://hl7.org/fhir/sid/icd-10");
        enabledUris.Should().NotContain("http://www.nlm.nih.gov/research/umls/rxnorm");
    }

    [Fact]
    public void CodeSystemSettings_EnabledSystemUris_WhenNoneEnabled_ReturnsEmpty()
    {
        // Arrange
        var settings = new CodeSystemSettings
        {
            Systems = new List<ConfiguredCodeSystem>
            {
                new("http://snomed.info/sct", "SNOMED CT", false),
                new("http://loinc.org", "LOINC", false)
            }
        };

        // Act
        var enabledUris = settings.EnabledSystemUris.ToList();

        // Assert
        enabledUris.Should().BeEmpty();
    }

    [Fact]
    public void CodeSystemSettings_JsonSerialization_RoundTrips()
    {
        // Arrange
        var settings = new CodeSystemSettings
        {
            Systems = new List<ConfiguredCodeSystem>
            {
                new("http://snomed.info/sct", "SNOMED CT", true),
                new("http://loinc.org", "LOINC", true)
            }
        };

        // Act
        var json = JsonSerializer.Serialize(settings);
        var decoded = JsonSerializer.Deserialize<CodeSystemSettings>(json);

        // Assert
        decoded.Should().NotBeNull();
        decoded!.Systems.Should().HaveCount(2);
        decoded.Systems.Should().Contain(s => s.Uri == "http://snomed.info/sct" && s.Enabled);
        decoded.Systems.Should().Contain(s => s.Uri == "http://loinc.org" && s.Enabled);
    }

    [Fact]
    public void CodeSystemSettings_PathToCodeSystems_ReturnsValidPath()
    {
        // Act
        var path = CodeSystemSettings.PathToCodeSystems();

        // Assert
        path.Should().NotBeNullOrWhiteSpace();
        path.Should().EndWith("codesystems.json");
        path.Should().Contain("Codeagogo");
    }

    #endregion
}
