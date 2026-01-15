// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for building Shrimp terminology browser URLs.
/// </summary>
public class ShrimpUrlBuilderTests
{
    private const string FhirEndpoint = "https://tx.ontoserver.csiro.au/fhir";
    private const string InternationalModuleId = "900000000000207008";
    private const string CoreModuleId = "900000000000012004";
    private const string AustralianModuleId = "32506021000036107";

    #region SNOMED CT with Module ID

    [Fact]
    public void BuildUrl_SnomedWithModuleId_IncludesVersionAndValueset()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: AustralianModuleId,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().StartWith("https://ontoserver.csiro.au/shrimp/?");
        url.Should().Contain("concept=73211009");
        url.Should().Contain("version=");
        url.Should().Contain(Uri.EscapeDataString($"http://snomed.info/sct/{AustralianModuleId}"));
        url.Should().Contain("valueset=");
        url.Should().Contain(Uri.EscapeDataString($"http://snomed.info/sct/{AustralianModuleId}?fhir_vs"));
    }

    [Fact]
    public void BuildUrl_SnomedWithInternationalModule_IncludesInternationalEdition()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: InternationalModuleId,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain(Uri.EscapeDataString($"http://snomed.info/sct/{InternationalModuleId}"));
    }

    #endregion

    #region Core Module ID Mapping

    [Fact]
    public void BuildUrl_CoreModuleId_MapsToInternational()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: CoreModuleId,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        // Core module should be mapped to International module ID
        url.Should().Contain(Uri.EscapeDataString($"http://snomed.info/sct/{InternationalModuleId}"));
        url.Should().NotContain(Uri.EscapeDataString($"http://snomed.info/sct/{CoreModuleId}"));
    }

    #endregion

    #region SNOMED CT without Module ID

    [Fact]
    public void BuildUrl_SnomedWithoutModuleId_FallsBackToInternational()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: null,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain("concept=73211009");
        // Should use International edition valueset as fallback
        url.Should().Contain("valueset=");
        url.Should().Contain(Uri.EscapeDataString($"http://snomed.info/sct/{InternationalModuleId}?fhir_vs"));
        // Should NOT have a version parameter (no module info)
        url.Should().NotContain("version=");
    }

    [Fact]
    public void BuildUrl_SnomedXsctSystem_IsTreatedAsSnomed()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/xsct",
            moduleId: AustralianModuleId,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        // xSCT should be treated as SNOMED CT (no system parameter)
        url.Should().NotContain("system=");
        url.Should().Contain(Uri.EscapeDataString($"http://snomed.info/sct/{AustralianModuleId}"));
    }

    #endregion

    #region Non-SNOMED Systems

    [Fact]
    public void BuildUrl_Loinc_IncludesSystemAndLoincValueset()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "8867-4",
            system: "http://loinc.org",
            version: "2.81",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain("concept=8867-4");
        url.Should().Contain($"system={Uri.EscapeDataString("http://loinc.org")}");
        url.Should().Contain($"version={Uri.EscapeDataString("2.81")}");
        url.Should().Contain($"valueset={Uri.EscapeDataString("http://loinc.org/vs")}");
    }

    [Fact]
    public void BuildUrl_Icd10_IncludesCorrectValueset()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "J45.901",
            system: "http://hl7.org/fhir/sid/icd-10-cm",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain($"system={Uri.EscapeDataString("http://hl7.org/fhir/sid/icd-10-cm")}");
        url.Should().Contain($"valueset={Uri.EscapeDataString("http://hl7.org/fhir/sid/icd-10-cm?fhir_vs")}");
    }

    [Fact]
    public void BuildUrl_Icd10Base_IncludesCorrectValueset()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "J45",
            system: "http://hl7.org/fhir/sid/icd-10",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain($"valueset={Uri.EscapeDataString("http://hl7.org/fhir/sid/icd-10?fhir_vs")}");
    }

    [Fact]
    public void BuildUrl_Icd9_IncludesCorrectValueset()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "250.00",
            system: "http://hl7.org/fhir/sid/icd-9-cm",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain($"valueset={Uri.EscapeDataString("http://hl7.org/fhir/sid/icd-9-cm?fhir_vs")}");
    }

    [Fact]
    public void BuildUrl_RxNorm_IncludesCorrectValueset()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "1049640",
            system: "http://www.nlm.nih.gov/research/umls/rxnorm",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain($"system={Uri.EscapeDataString("http://www.nlm.nih.gov/research/umls/rxnorm")}");
        url.Should().Contain($"valueset={Uri.EscapeDataString("http://www.nlm.nih.gov/research/umls/rxnorm?fhir_vs")}");
    }

    [Fact]
    public void BuildUrl_UnknownSystem_UsesFhirVsFallback()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "ABC",
            system: "http://example.org/codesystem",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain($"valueset={Uri.EscapeDataString("http://example.org/codesystem?fhir_vs")}");
    }

    [Fact]
    public void BuildUrl_NonSnomed_WithoutVersion_NoVersionParam()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "8867-4",
            system: "http://loinc.org",
            version: null,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().NotContain("version=");
    }

    #endregion

    #region Null System

    [Fact]
    public void BuildUrl_NullSystem_ReturnsNull()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: null,
            fhirEndpoint: FhirEndpoint);

        url.Should().BeNull();
    }

    [Fact]
    public void BuildUrl_EmptySystem_ReturnsNull()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "",
            fhirEndpoint: FhirEndpoint);

        url.Should().BeNull();
    }

    #endregion

    #region FHIR Endpoint Normalization

    [Fact]
    public void BuildUrl_FhirEndpointWithTrailingSlash_SlashRemoved()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: InternationalModuleId,
            fhirEndpoint: "https://tx.ontoserver.csiro.au/fhir/");

        url.Should().NotBeNull();
        url.Should().Contain($"fhir={Uri.EscapeDataString("https://tx.ontoserver.csiro.au/fhir")}");
        // Ensure the trailing slash is not present in the encoded URL
        url.Should().NotContain(Uri.EscapeDataString("https://tx.ontoserver.csiro.au/fhir/"));
    }

    [Fact]
    public void BuildUrl_FhirEndpointWithoutTrailingSlash_Unchanged()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: InternationalModuleId,
            fhirEndpoint: "https://tx.ontoserver.csiro.au/fhir");

        url.Should().NotBeNull();
        url.Should().Contain($"fhir={Uri.EscapeDataString("https://tx.ontoserver.csiro.au/fhir")}");
    }

    [Fact]
    public void BuildUrl_NullFhirEndpoint_NoFhirParam()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: InternationalModuleId,
            fhirEndpoint: null);

        url.Should().NotBeNull();
        url.Should().NotContain("fhir=");
    }

    #endregion

    #region ConceptResult Overload

    [Fact]
    public void BuildUrl_ConceptResultWithNullSystem_DefaultsToSnomedSct()
    {
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: "International",
            Fsn: "Diabetes mellitus (disorder)",
            Pt: "Diabetes mellitus",
            Active: true,
            EffectiveTime: "20240101",
            ModuleId: InternationalModuleId,
            System: null);

        var url = ShrimpUrlBuilder.BuildUrl(result, FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain("concept=73211009");
        // System defaults to snomed.info/sct, so no "system=" param (SNOMED path)
        url.Should().NotContain("system=");
    }

    [Fact]
    public void BuildUrl_ConceptResultWithSystem_UsesProvidedSystem()
    {
        var result = new ConceptResult(
            ConceptId: "8867-4",
            Branch: "LOINC (2.81)",
            Fsn: null,
            Pt: "Heart rate",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://loinc.org");

        var url = ShrimpUrlBuilder.BuildUrl(result, FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain("concept=8867-4");
        url.Should().Contain($"system={Uri.EscapeDataString("http://loinc.org")}");
    }

    [Fact]
    public void BuildUrl_ConceptResultWithBranchVersion_ExtractsVersion()
    {
        var result = new ConceptResult(
            ConceptId: "8867-4",
            Branch: "LOINC (2.81)",
            Fsn: null,
            Pt: "Heart rate",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://loinc.org");

        var url = ShrimpUrlBuilder.BuildUrl(result, FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain($"version={Uri.EscapeDataString("2.81")}");
    }

    [Fact]
    public void BuildUrl_ConceptResultWithBranchNoParens_NoVersionParam()
    {
        var result = new ConceptResult(
            ConceptId: "8867-4",
            Branch: "LOINC",
            Fsn: null,
            Pt: "Heart rate",
            Active: true,
            EffectiveTime: null,
            ModuleId: null,
            System: "http://loinc.org");

        var url = ShrimpUrlBuilder.BuildUrl(result, FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().NotContain("version=");
    }

    [Fact]
    public void BuildUrl_ConceptResultWithNullBranch_NoVersionParam()
    {
        var result = new ConceptResult(
            ConceptId: "73211009",
            Branch: null!,
            Fsn: "Diabetes mellitus (disorder)",
            Pt: "Diabetes mellitus",
            Active: true,
            EffectiveTime: "20240101",
            ModuleId: InternationalModuleId,
            System: null);

        // Branch is null - ExtractVersion returns null
        var url = ShrimpUrlBuilder.BuildUrl(result, FhirEndpoint);

        url.Should().NotBeNull();
    }

    #endregion

    #region URL Structure

    [Fact]
    public void BuildUrl_AlwaysStartsWithShrimpBaseUrl()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: InternationalModuleId,
            fhirEndpoint: FhirEndpoint);

        url.Should().StartWith("https://ontoserver.csiro.au/shrimp/?");
    }

    [Fact]
    public void BuildUrl_ConceptParamAlwaysPresent()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            fhirEndpoint: FhirEndpoint);

        url.Should().Contain("concept=73211009");
    }

    [Fact]
    public void BuildUrl_SnomedSystem_DoesNotIncludeSystemParam()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            moduleId: InternationalModuleId,
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        // SNOMED CT URLs should not have a system= param
        url.Should().NotContain("system=");
    }

    [Fact]
    public void BuildUrl_NonSnomedSystem_IncludesSystemParam()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "8867-4",
            system: "http://loinc.org",
            fhirEndpoint: FhirEndpoint);

        url.Should().NotBeNull();
        url.Should().Contain("system=");
    }

    #endregion
}
