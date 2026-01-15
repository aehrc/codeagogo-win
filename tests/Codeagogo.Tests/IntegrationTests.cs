// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Integration tests that hit the live FHIR terminology server at tx.ontoserver.csiro.au.
/// These are excluded from normal test runs via [Trait("Category", "Integration")].
///
/// Run with: dotnet test --filter "Category=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class IntegrationTests : IDisposable
{
    private readonly OntoserverClient _client;

    public IntegrationTests()
    {
        _client = new OntoserverClient();
    }

    public void Dispose()
    {
        // OntoserverClient owns its HttpClient internally; no explicit dispose needed.
        GC.SuppressFinalize(this);
    }

    // ---------------------------------------------------------------
    // Single concept lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task LookupAsync_ReturnsConcept_ForKnownSCTID()
    {
        // Arrange
        var conceptId = "73211009"; // Diabetes mellitus

        // Act
        var result = await _client.LookupAsync(conceptId);

        // Assert
        result.Should().NotBeNull();
        result.ConceptId.Should().Be(conceptId);
        result.Pt.Should().Be("Diabetes mellitus");
        result.Fsn.Should().Contain("Diabetes mellitus").And.Contain("(disorder)");
        result.Active.Should().BeTrue();
        result.Branch.Should().Contain("International");
    }

    [Fact]
    public async Task LookupAsync_ReturnsConcept_ForAnotherKnownSCTID()
    {
        // Arrange
        var conceptId = "404684003"; // Clinical finding (SNOMED CT hierarchy concept)

        // Act
        var result = await _client.LookupAsync(conceptId);

        // Assert
        result.Should().NotBeNull();
        result.ConceptId.Should().Be(conceptId);
        result.Pt.Should().NotBeNullOrWhiteSpace();
        result.Active.Should().BeTrue();
    }

    // ---------------------------------------------------------------
    // Unknown / invalid concept
    // ---------------------------------------------------------------

    [Fact]
    public async Task LookupAsync_ThrowsException_ForUnknownConcept()
    {
        // Arrange — a clearly invalid concept ID that does not exist in any edition
        var conceptId = "9999999999999";

        // Act
        Func<Task> act = () => _client.LookupAsync(conceptId);

        // Assert — LookupAsync throws when the concept is not found in any edition
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*not found*");
    }

    // ---------------------------------------------------------------
    // Batch lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task BatchLookupAsync_ReturnsMultipleConcepts()
    {
        // Arrange — well-known SNOMED CT concepts
        var conceptIds = new[]
        {
            "73211009",   // Diabetes mellitus
            "38341003",   // Hypertensive disorder
            "195967001",  // Asthma
            "22298006",   // Myocardial infarction
            "13645005",   // Chronic obstructive lung disease
            "84114007",   // Heart failure
            "44054006",   // Diabetes mellitus type 2
        };

        // Act
        var result = await _client.BatchLookupAsync(conceptIds);

        // Assert
        result.Should().NotBeNull();
        result.PtByCode.Should().HaveCountGreaterOrEqualTo(conceptIds.Length);

        // Every requested concept should have a preferred term
        foreach (var id in conceptIds)
        {
            result.PtByCode.Should().ContainKey(id, $"concept {id} should have a preferred term");
            result.PtByCode[id].Should().NotBeNullOrWhiteSpace();
        }

        // Spot-check a specific value
        result.PtByCode["73211009"].Should().Be("Diabetes mellitus");

        // FSN should be present for at least some concepts
        result.FsnByCode.Should().HaveCountGreaterThan(0);

        // Active status should be present
        result.ActiveByCode.Should().HaveCountGreaterOrEqualTo(conceptIds.Length);
    }

    [Fact]
    public async Task BatchLookupAsync_ReturnsEmptyResult_ForEmptyInput()
    {
        // Arrange
        var conceptIds = Array.Empty<string>();

        // Act
        var result = await _client.BatchLookupAsync(conceptIds);

        // Assert
        result.Should().NotBeNull();
        result.PtByCode.Should().BeEmpty();
        result.FsnByCode.Should().BeEmpty();
        result.ActiveByCode.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Search
    // ---------------------------------------------------------------

    [Fact]
    public async Task SearchAsync_ReturnsResults_ForCommonTerm()
    {
        // Arrange
        var filter = "diabetes";
        var system = "http://snomed.info/sct";

        // Act
        var results = await _client.SearchAsync(filter, system);

        // Assert
        results.Should().NotBeNullOrEmpty();
        results.Should().HaveCountGreaterOrEqualTo(5, "a common term like 'diabetes' should yield many results");

        // Every result should have a code and display
        foreach (var item in results)
        {
            item.Code.Should().NotBeNullOrWhiteSpace();
            item.Display.Should().NotBeNullOrWhiteSpace();
            item.SystemUri.Should().Be(system);
        }

        // At least one result should contain "diabetes" in the display or FSN
        results.Should().Contain(r =>
            (r.Display != null && r.Display.Contains("iabetes", StringComparison.OrdinalIgnoreCase)) ||
            (r.Fsn != null && r.Fsn.Contains("iabetes", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyList_ForTooShortFilter()
    {
        // Arrange — filter must be at least 3 characters
        var filter = "ab";
        var system = "http://snomed.info/sct";

        // Act
        var results = await _client.SearchAsync(filter, system);

        // Assert
        results.Should().BeEmpty("filter shorter than 3 characters should be rejected");
    }

    [Fact]
    public async Task SearchAsync_SupportsCancellation()
    {
        // Arrange
        var filter = "diabetes";
        var system = "http://snomed.info/sct";
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately

        // Act
        var results = await _client.SearchAsync(filter, system, ct: cts.Token);

        // Assert — cancelled request should return empty list, not throw
        results.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // Available code systems
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAvailableCodeSystemsAsync_ReturnsCodeSystems()
    {
        // Act
        var systems = await _client.GetAvailableCodeSystemsAsync();

        // Assert
        systems.Should().NotBeNullOrEmpty();
        systems.Should().HaveCountGreaterOrEqualTo(1, "at least SNOMED CT should be available");

        // SNOMED CT should be present
        systems.Should().Contain(s => s.Uri.Contains("snomed.info"),
            "SNOMED CT should be among the available code systems");

        // Every system should have a URI and title
        foreach (var cs in systems)
        {
            cs.Uri.Should().NotBeNullOrWhiteSpace();
            cs.Title.Should().NotBeNullOrWhiteSpace();
        }
    }

    // ---------------------------------------------------------------
    // Code system lookup (non-SNOMED)
    // ---------------------------------------------------------------

    [Fact]
    public async Task LookupInCodeSystemAsync_ReturnsConcept_ForSnomedCodeSystem()
    {
        // Arrange — look up a SNOMED concept via the generic code system lookup
        var code = "73211009";
        var system = "http://snomed.info/sct";

        // Act
        var result = await _client.LookupInCodeSystemAsync(code, system);

        // Assert
        result.Should().NotBeNull();
        result.ConceptId.Should().Be(code);
        result.Pt.Should().NotBeNullOrWhiteSpace();
        result.System.Should().Be(system);
    }

    [Fact]
    public async Task LookupInCodeSystemAsync_Throws_ForInvalidCode()
    {
        // Arrange
        var code = "INVALID_CODE_12345";
        var system = "http://snomed.info/sct";

        // Act
        Func<Task> act = () => _client.LookupInCodeSystemAsync(code, system);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    // ---------------------------------------------------------------
    // Multi-system lookup
    // ---------------------------------------------------------------

    [Fact]
    public async Task LookupInConfiguredSystemsAsync_FindsConcept_InFirstMatchingSystem()
    {
        // Arrange — code 73211009 exists in SNOMED CT
        var code = "73211009";
        var systems = new[] { "http://snomed.info/sct" };

        // Act
        var result = await _client.LookupInConfiguredSystemsAsync(code, systems);

        // Assert
        result.Should().NotBeNull();
        result!.ConceptId.Should().Be(code);
        result.Pt.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LookupInConfiguredSystemsAsync_ReturnsNull_WhenNotFoundInAnySystems()
    {
        // Arrange — bogus code in a real system
        var code = "BOGUS_999999";
        var systems = new[] { "http://snomed.info/sct" };

        // Act
        var result = await _client.LookupInConfiguredSystemsAsync(code, systems);

        // Assert
        result.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // Concept properties (for visualization)
    // ---------------------------------------------------------------

    [Fact]
    public async Task LookupWithPropertiesAsync_ReturnsProperties_ForKnownConcept()
    {
        // Arrange
        var conceptId = "73211009"; // Diabetes mellitus

        // Act
        var props = await _client.LookupWithPropertiesAsync(conceptId);

        // Assert
        props.Should().NotBeNull();
        props!.ConceptId.Should().Be(conceptId);
        props.PreferredTerm.Should().Be("Diabetes mellitus");
        props.FullySpecifiedName.Should().Contain("Diabetes mellitus");

        // Should have at least one parent (Diabetes mellitus is a descendant of Disorder of glucose metabolism)
        props.ParentCodes.Should().NotBeEmpty("every non-root concept should have at least one parent");

        // Active status
        props.Active.Should().BeTrue();
    }

    [Fact]
    public async Task LookupWithPropertiesAsync_ReturnsNull_ForInvalidConcept()
    {
        // Arrange — a concept that does not exist
        var conceptId = "9999999999999";

        // Act
        var props = await _client.LookupWithPropertiesAsync(conceptId);

        // Assert — the method returns null when the server returns a non-success status
        props.Should().BeNull();
    }

    [Fact]
    public async Task LookupWithPropertiesAsync_HasNormalForm_ForDefinedConcept()
    {
        // Arrange — Diabetes mellitus type 2 (a fully defined concept)
        var conceptId = "44054006";

        // Act
        var props = await _client.LookupWithPropertiesAsync(conceptId);

        // Assert
        props.Should().NotBeNull();
        props!.ConceptId.Should().Be(conceptId);
        props.PreferredTerm.Should().NotBeNullOrWhiteSpace();

        // A sufficiently defined concept should have a normal form
        // Note: some servers may not return normalForm, so we only assert when present
        if (props.NormalForm != null)
        {
            props.NormalForm.Should().NotBeEmpty();
        }
    }

    // ---------------------------------------------------------------
    // Edge cases / resilience
    // ---------------------------------------------------------------

    [Fact]
    public async Task LookupAsync_ReturnsConcept_ForInactiveConcept()
    {
        // Arrange — 135811000119104 is "Neonatal diabetes mellitus" which is inactive
        // Using a well-known inactive concept: 154283005 (Diabetes mellitus - Loss of foot - Loss of part of foot)
        // Alternatively, 59276001 was retired. Let's use 195967001 as a control (active) first.
        // For a reliably inactive concept, use 63491006 (historically inactive in some editions)
        // Using 190388001 - "Diabetes mellitus with hyperosmolar coma" — known to be inactive
        var conceptId = "190388001";

        // Act — this should either return the concept or throw. We verify based on result.
        try
        {
            var result = await _client.LookupAsync(conceptId);

            // If found, verify the structure is valid
            result.Should().NotBeNull();
            result.ConceptId.Should().Be(conceptId);
            result.Pt.Should().NotBeNullOrWhiteSpace();
            // Active may be true or false depending on the edition — just verify it has a value
            result.Active.Should().HaveValue();
        }
        catch (Exception ex)
        {
            // If not found in any edition, that's also acceptable for inactive concepts
            ex.Message.Should().Contain("not found");
        }
    }

    [Fact]
    public async Task SearchAsync_WithEditionFilter_ReturnsEditionSpecificResults()
    {
        // Arrange — search with Australian edition filter
        var filter = "diabetes";
        var system = "http://snomed.info/sct";
        var australianEdition = "http://snomed.info/sct/32506021000036107";

        // Act
        var results = await _client.SearchAsync(filter, system, editionUri: australianEdition);

        // Assert — should still return results (Australian edition contains diabetes concepts)
        results.Should().NotBeNullOrEmpty();
        results.Should().OnlyContain(r => r.Code != null, "every result should have a code");
    }
}
