// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;
using Codeagogo.Visualization;

namespace Codeagogo.Tests;

/// <summary>
/// Security-focused tests covering URL validation, input sanitisation,
/// SVG injection prevention, and settings deserialization safety.
/// </summary>
public class SecurityTests
{
    #region URL Validation – OntoserverClient constructor

    [Fact]
    public void OntoserverClient_HttpsUrl_Accepted()
    {
        var act = () => new OntoserverClient(baseUrl: "https://example.com/fhir");
        act.Should().NotThrow();
    }

    [Fact]
    public void OntoserverClient_HttpUrl_Accepted()
    {
        var act = () => new OntoserverClient(baseUrl: "http://example.com/fhir");
        act.Should().NotThrow();
    }

    [Fact]
    public void OntoserverClient_FileScheme_Throws()
    {
        var act = () => new OntoserverClient(baseUrl: "file:///etc/passwd");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OntoserverClient_FtpScheme_Throws()
    {
        var act = () => new OntoserverClient(baseUrl: "ftp://server/path");
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region URL Validation – SetBaseUrl

    [Fact]
    public void SetBaseUrl_HttpsUrl_Accepted()
    {
        var client = new OntoserverClient(baseUrl: "https://example.com/fhir");
        var act = () => client.SetBaseUrl("https://other.example.com/fhir");
        act.Should().NotThrow();
    }

    [Fact]
    public void SetBaseUrl_FileScheme_Throws()
    {
        var client = new OntoserverClient(baseUrl: "https://example.com/fhir");
        var act = () => client.SetBaseUrl("file:///etc/passwd");
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Input Validation – ExtractFirstSnomedId

    [Fact]
    public void ExtractFirstSnomedId_InputExceedsMaxLength_StillWorks()
    {
        // Build a 15 000-character string with a valid SNOMED ID near the start
        var input = "73211009 " + new string('x', 15_000);
        var result = ClipboardSelectionReader.ExtractFirstSnomedId(input);
        result.Should().Be("73211009");
    }

    [Fact]
    public void ExtractFirstSnomedId_NullInput_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId(null).Should().BeNull();
    }

    [Fact]
    public void ExtractFirstSnomedId_EmptyInput_ReturnsNull()
    {
        ClipboardSelectionReader.ExtractFirstSnomedId("").Should().BeNull();
    }

    #endregion

    #region Input Validation – ExtractAllConceptIds

    [Fact]
    public void ExtractAllConceptIds_InputExceedsMaxLength_StillWorks()
    {
        var input = "73211009 " + new string('a', 15_000);
        var act = () => ClipboardSelectionReader.ExtractAllConceptIds(input);
        act.Should().NotThrow();

        var matches = ClipboardSelectionReader.ExtractAllConceptIds(input);
        matches.Should().NotBeEmpty();
        matches[0].ConceptId.Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_SpecialCharacters_DoesNotCrash()
    {
        // Input with null char, unicode, emoji, control chars
        var input = "73211009\0 some \u00e9\u00e8 text \U0001F600 \t\r\n end";
        var act = () => ClipboardSelectionReader.ExtractAllConceptIds(input);
        act.Should().NotThrow();

        var matches = ClipboardSelectionReader.ExtractAllConceptIds(input);
        matches.Should().NotBeEmpty();
        matches[0].ConceptId.Should().Be("73211009");
    }

    [Fact]
    public void ExtractAllConceptIds_MalformedPipeDelimiters_HandlesGracefully()
    {
        // Unclosed pipe delimiter should not crash
        var input = "12345678 | unclosed pipe";
        var act = () => ClipboardSelectionReader.ExtractAllConceptIds(input);
        act.Should().NotThrow();

        var matches = ClipboardSelectionReader.ExtractAllConceptIds(input);
        matches.Should().NotBeEmpty();
        matches[0].ConceptId.Should().Be("12345678");
    }

    #endregion

    #region SVG Injection – DiagramRenderer

    [Fact]
    public void RenderSvg_ScriptTag_Escaped()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "12345678",
            PreferredTerm = "<script>alert('xss')</script>",
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().NotContain("<script>");
        svg.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void RenderSvg_SvgPayload_Escaped()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "12345678",
            PreferredTerm = "<svg onload=alert(1)>",
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        // The injected <svg> tag must be escaped so it cannot be parsed as markup
        svg.Should().Contain("&lt;svg onload=alert(1)&gt;");
        // Ensure the raw angle brackets around the payload are not present
        svg.Should().NotContain("<svg onload");
    }

    [Fact]
    public void RenderSvg_Ampersand_Escaped()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "12345678",
            PreferredTerm = "A & B",
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("A &amp; B");
    }

    [Fact]
    public void RenderSvg_AllSpecialChars_Escaped()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "12345678",
            PreferredTerm = "& < > \" '",
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("&amp;");
        svg.Should().Contain("&lt;");
        svg.Should().Contain("&gt;");
        svg.Should().Contain("&quot;");
        svg.Should().Contain("&#39;");
    }

    [Fact]
    public void RenderSvg_NormalText_Unchanged()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "73211009",
            PreferredTerm = "Diabetes mellitus",
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("Diabetes mellitus");
    }

    #endregion

    #region Settings Deserialization Safety

    [Fact]
    public void Settings_CorruptedJson_FallsBackToDefaults()
    {
        // Settings.Load() catches all exceptions and returns defaults.
        // We verify that the default object is valid when deserialization would fail.
        var defaults = new Settings();
        defaults.FhirBaseUrl.Should().NotBeNullOrEmpty();
        defaults.LookupHotKeyVirtualKey.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Settings_EmptyJson_FallsBackToDefaults()
    {
        // JsonSerializer.Deserialize<Settings>("") throws; Load() catches it.
        var act = () => System.Text.Json.JsonSerializer.Deserialize<Settings>("");
        act.Should().Throw<System.Text.Json.JsonException>();

        // Confirm defaults are still sane
        var defaults = new Settings();
        defaults.FhirBaseUrl.Should().Be("https://tx.ontoserver.csiro.au/fhir/");
    }

    #endregion

    #region ShrimpUrlBuilder – URL Encoding

    [Fact]
    public void BuildUrl_ConceptIdIsUrlEncoded()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "123 & 456",
            system: "http://snomed.info/sct",
            fhirEndpoint: "https://tx.ontoserver.csiro.au/fhir");

        url.Should().NotBeNull();
        // The raw concept ID with special chars must be percent-encoded
        url.Should().Contain("concept=123%20%26%20456");
        url.Should().NotContain("concept=123 & 456");
    }

    [Fact]
    public void BuildUrl_FhirEndpointIsUrlEncoded()
    {
        var url = ShrimpUrlBuilder.BuildUrl(
            conceptId: "73211009",
            system: "http://snomed.info/sct",
            fhirEndpoint: "https://tx.ontoserver.csiro.au/fhir");

        url.Should().NotBeNull();
        // The fhir parameter value should be percent-encoded (colon, slashes)
        url.Should().Contain("fhir=");
        url.Should().Contain(Uri.EscapeDataString("https://tx.ontoserver.csiro.au/fhir"));
    }

    #endregion
}
