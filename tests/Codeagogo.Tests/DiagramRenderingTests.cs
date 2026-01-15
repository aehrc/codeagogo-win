// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using Codeagogo.Visualization;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// End-to-end tests for the diagram rendering pipeline.
/// Verifies the complete flow: normal form parsing → property replacement →
/// concept ID collection → enrichment → SVG rendering.
/// Uses real data from concept 37933011000036106 (Midazolam 5 mg/mL injection, ampoule).
/// </summary>
public class DiagramRenderingTests
{
    private const string MidazolamNormalForm =
        "=== 389105002|Product containing midazolam in parenteral dose form|:" +
        "411116001|Has manufactured dose form|=(129011000036109|Injection dose form|:" +
        "{736474004|Has dose form intended site|=738984000|Parenteral|})" +
        "{999000051000168108|Has total quantity unit|=258684004|milligram|," +
        "999000041000168106|Has total quantity value|=#5.0," +
        "999000021000168100|Has concentration strength value|=#5.0," +
        "732943007|Has basis of strength substance|=373476007|Midazolam|," +
        "999000031000168102|Has concentration strength unit|=(415777001|Unit of mass concentration|:" +
        "{700000071000036103|Has denominator units|=258773002|Milliliter|}," +
        "{700000091000036104|Has numerator units|=258684004|milligram|})," +
        "127489000|Has active ingredient|=373476007|Midazolam|}," +
        "{1142142004|Has pack size|=#1.0," +
        "774163005|Has pack size unit|=258773002|Milliliter|}," +
        "{30465011000036106|Has container type|=469844003|Ampule|}";

    private static readonly Dictionary<string, string> RelationshipProperties = new()
    {
        ["127489000"] = "373476007",
        ["411116001"] = "129011000036109",
        ["732943007"] = "373476007",
        ["774163005"] = "258773002",
        ["30465011000036106"] = "469844003",
        ["999000031000168102"] = "258798001",  // mg/mL — NOT 415777001
        ["999000051000168108"] = "258684004",
    };

    // Simulated server PTs for the pre-coordinated concepts
    private static readonly Dictionary<string, string> ServerPTs = new()
    {
        ["389105002"] = "Midazolam-containing product in parenteral dose form",
        ["258684004"] = "mg",
        ["373476007"] = "Midazolam",
        ["258798001"] = "mg/mL",        // the correct PT for the pre-coordinated concept
        ["258773002"] = "mL",
        ["469844003"] = "Ampoule",       // AU PT, not "Ampule"
        ["129011000036109"] = "Injection",
        ["415777001"] = "Unit of mass concentration",  // should NOT appear in final diagram
        ["738984000"] = "Parenteral",
        ["1142142004"] = "Has pack size",
        ["774163005"] = "Has pack size unit",
        ["999000051000168108"] = "Has total quantity unit",
        ["999000041000168106"] = "Has total quantity value",
        ["999000021000168100"] = "Has concentration strength value",
        ["732943007"] = "Has BoSS",
        ["999000031000168102"] = "Has concentration strength unit",
        ["127489000"] = "Has active ingredient",
        ["30465011000036106"] = "Has container type",
        ["411116001"] = "Has manufactured dose form",
        ["736474004"] = "Has dose form intended site",
    };

    private ConceptVisualizationData BuildVizData()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        var vizData = new ConceptVisualizationData
        {
            ConceptId = "37933011000036106",
            PreferredTerm = "Midazolam 5 mg/mL injection, ampoule",
            SufficientlyDefined = true
        };
        vizData.Parents.Add(new ConceptReference("389105002", null));
        vizData.UngroupedAttributes.AddRange(result.UngroupedAttributes);
        vizData.AttributeGroups.AddRange(result.Groups);
        return vizData;
    }

    [Fact]
    public void Step1_Parse_ConcentrationStrengthUnitIsFocusConcept()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        var allAttrs = result.Groups.SelectMany(g => g.Attributes).Concat(result.UngroupedAttributes);
        var attr = allAttrs.First(a => a.Type.ConceptId == "999000031000168102");
        attr.Value.ConceptId.Should().Be("415777001", "parser returns decomposed focus concept");
    }

    [Fact]
    public void Step2_Replace_ConcentrationStrengthUnitBecomesPreCoordinated()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes).Concat(vizData.UngroupedAttributes);
        var attr = allAttrs.First(a => a.Type.ConceptId == "999000031000168102");
        attr.Value.ConceptId.Should().Be("258798001", "should be replaced with pre-coordinated mg/mL");
    }

    [Fact]
    public void Step3_CollectIDs_Includes258798001_Not415777001()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        // Collect IDs the same way VisualizationWindow does
        var allConceptIds = new HashSet<string>();
        foreach (var p in vizData.Parents)
            allConceptIds.Add(p.ConceptId);
        foreach (var a in vizData.UngroupedAttributes)
        {
            allConceptIds.Add(a.Type.ConceptId);
            allConceptIds.Add(a.Value.ConceptId);
        }
        foreach (var g in vizData.AttributeGroups)
            foreach (var a in g.Attributes)
            {
                allConceptIds.Add(a.Type.ConceptId);
                allConceptIds.Add(a.Value.ConceptId);
            }
        allConceptIds.Remove("37933011000036106");
        allConceptIds.Remove("concrete");
        allConceptIds.Remove("nested");

        allConceptIds.Should().Contain("258798001", "pre-coordinated mg/mL should be in lookup list");
        allConceptIds.Should().NotContain("415777001", "decomposed focus concept should NOT be in lookup list");
    }

    [Fact]
    public void Step4_Enrich_ShowsMgMl_NotUnitOfMassConcentration()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        // Simulate enrichment with server PTs
        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes).Concat(vizData.UngroupedAttributes);
        var attr = allAttrs.First(a => a.Type.ConceptId == "999000031000168102");
        attr.Value.Term.Should().Be("mg/mL");
        attr.Value.Term.Should().NotBe("Unit of mass concentration");
    }

    [Fact]
    public void Step4_Enrich_ShowsAmpoule_NotAmpule()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes).Concat(vizData.UngroupedAttributes);
        var attr = allAttrs.First(a => a.Type.ConceptId == "30465011000036106");
        attr.Value.Term.Should().Be("Ampoule");
    }

    [Fact]
    public void Step4_Enrich_ConcreteValuesPreserved()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes).Concat(vizData.UngroupedAttributes);
        var concretes = allAttrs.Where(a => a.Value.ConceptId == "concrete").ToList();
        concretes.Should().NotBeEmpty();
        concretes.Should().AllSatisfy(a => a.Value.Term.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void Step5_SVG_ContainsMgMl()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var defMap = new Dictionary<string, bool> { ["37933011000036106"] = true };
        var svg = DiagramRenderer.RenderSvg(vizData, defMap);

        svg.Should().Contain("mg/mL", "SVG should show mg/mL");
        svg.Should().NotContain("Unit of mass concentration");
    }

    [Fact]
    public void Step5_SVG_ContainsAmpoule()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var defMap = new Dictionary<string, bool> { ["37933011000036106"] = true };
        var svg = DiagramRenderer.RenderSvg(vizData, defMap);

        svg.Should().Contain("Ampoule");
        svg.Should().NotContain("Ampule");
    }

    [Fact]
    public void Step5_SVG_ContainsGreenConcreteValues()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var defMap = new Dictionary<string, bool> { ["37933011000036106"] = true };
        var svg = DiagramRenderer.RenderSvg(vizData, defMap);

        svg.Should().Contain("#CCFFCC", "concrete values should have green background");
        svg.Should().Contain("5.0");
        svg.Should().Contain("1.0");
    }

    [Fact]
    public void Step5_SVG_DoesNotContainConcreteAsText()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var defMap = new Dictionary<string, bool> { ["37933011000036106"] = true };
        var svg = DiagramRenderer.RenderSvg(vizData, defMap);

        // "concrete" should not appear as visible text in the SVG
        svg.Should().NotContain(">concrete<");
    }

    [Fact]
    public void Parse_ConcreteValues_HaveNumericText()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        var allAttrs = result.Groups.SelectMany(g => g.Attributes).Concat(result.UngroupedAttributes);
        var concretes = allAttrs.Where(a => a.Value.ConceptId == "concrete").ToList();

        concretes.Should().NotBeEmpty("normal form contains concrete values like #5.0 and #1.0");
        concretes.Should().AllSatisfy(a =>
            a.Value.Term.Should().NotBeNullOrEmpty("concrete values must have their numeric text parsed"));
    }

    [Fact]
    public void Enrich_PreservesConcreteValueTerms_WhenBatchHasNoEntry()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        // Empty batch — no server PTs available
        var emptyBatch = new BatchLookupResult(new(), new(), new());
        vizData = EnrichWithTerms(vizData, emptyBatch);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes).Concat(vizData.UngroupedAttributes);
        var concretes = allAttrs.Where(a => a.Value.ConceptId == "concrete").ToList();

        concretes.Should().NotBeEmpty();
        concretes.Should().AllSatisfy(a =>
            a.Value.Term.Should().NotBeNullOrEmpty(
                "concrete value terms must survive enrichment even when the batch has no entries"));
    }

    [Fact]
    public void Enrich_PrefersServerPT_OverNormalFormTerm()
    {
        var vizData = BuildVizData();
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, RelationshipProperties);

        // The normal form has "milligram" for 258684004, but the server PT is "mg"
        var batch = new BatchLookupResult(new(ServerPTs), new(), new());
        vizData = EnrichWithTerms(vizData, batch);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes).Concat(vizData.UngroupedAttributes);
        var attr = allAttrs.First(a => a.Value.ConceptId == "258684004");
        attr.Value.Term.Should().Be("mg",
            "server PT 'mg' should replace normal form term 'milligram'");
    }

    // Replicates the EnrichWithTerms logic from VisualizationWindow
    private static ConceptVisualizationData EnrichWithTerms(ConceptVisualizationData data, BatchLookupResult batch)
    {
        ConceptReference EnrichRef(ConceptReference r)
        {
            if (r.ConceptId == "concrete")
                return r;
            if (batch.PtByCode.TryGetValue(r.ConceptId, out var pt) && !string.IsNullOrEmpty(pt))
                return new ConceptReference(r.ConceptId, pt);
            return string.IsNullOrEmpty(r.Term)
                ? new ConceptReference(r.ConceptId, r.ConceptId)
                : r;
        }

        var enrichedParents = data.Parents.Select(p =>
        {
            if (batch.PtByCode.TryGetValue(p.ConceptId, out var pt) && !string.IsNullOrEmpty(pt))
                return new ConceptReference(p.ConceptId, pt);
            return string.IsNullOrEmpty(p.Term)
                ? new ConceptReference(p.ConceptId, p.ConceptId)
                : p;
        }).ToList();

        var enrichedUngrouped = data.UngroupedAttributes.Select(a =>
            new ConceptAttribute(EnrichRef(a.Type), EnrichRef(a.Value))).ToList();

        var enrichedGroups = data.AttributeGroups.Select(g =>
        {
            var eg = new AttributeGroup { GroupNumber = g.GroupNumber };
            eg.Attributes.AddRange(g.Attributes.Select(a =>
                new ConceptAttribute(EnrichRef(a.Type), EnrichRef(a.Value))));
            return eg;
        }).ToList();

        var enriched = new ConceptVisualizationData
        {
            ConceptId = data.ConceptId,
            PreferredTerm = data.PreferredTerm,
            FullySpecifiedName = data.FullySpecifiedName,
            SufficientlyDefined = data.SufficientlyDefined
        };
        enriched.Parents.AddRange(enrichedParents);
        enriched.UngroupedAttributes.AddRange(enrichedUngrouped);
        enriched.AttributeGroups.AddRange(enrichedGroups);
        return enriched;
    }
}

/// <summary>
/// Integration tests that hit the real FHIR server to verify display terms.
/// </summary>
[Trait("Category", "Integration")]
public class DiagramDisplayTermIntegrationTests
{
    private readonly OntoserverClient _client = new(baseUrl: "https://tx.ontoserver.csiro.au/fhir/");

    [Fact]
    public async Task LookupDefaultEditionAsync_Ampule469844003_ReturnsAUPreferredTerm()
    {
        // 469844003 is "Ampule" in International, "Ampoule" in SCTAU
        // LookupDefaultEditionAsync uses no version — server returns default edition PT
        var result = await _client.LookupDefaultEditionAsync("469844003");

        result.Should().NotBeNull();
        result!.Pt.Should().NotBeNull();

        Console.WriteLine($"469844003 PT: '{result.Pt}' (edition: {result.Branch})");

        result.Pt.Should().Be("Ampoule",
            $"Server default edition should return 'Ampoule' (AU) not 'Ampule' (INT). Got PT='{result.Pt}' from edition={result.Branch}");
    }

    [Fact]
    public async Task LookupAsync_258798001_ReturnsMgMl()
    {
        var result = await _client.LookupAsync("258798001");
        result.Should().NotBeNull();
        result.Pt.Should().Be("mg/mL");
    }

    [Fact]
    public async Task LookupWithPropertiesAsync_469844003_ChecksWhichEdition()
    {
        // Check what LookupWithPropertiesAsync returns — it tries International first
        var result = await _client.LookupWithPropertiesAsync("469844003");
        result.Should().NotBeNull();
        
        Console.WriteLine($"LookupWithProperties 469844003 PT: '{result!.PreferredTerm}'");
    }

    [Fact]
    public async Task LookupAsync_469844003_ReturnsInternationalPT()
    {
        // LookupAsync tries International first, then no-version.
        // For 469844003 (an International concept), International will succeed
        // and return "Ampule" — NOT "Ampoule" from the AU edition.
        // This is expected: LookupAsync stops at International and never falls through
        // to the no-version endpoint. The diagram code uses LookupDefaultEditionAsync
        // (no version parameter) instead to get the server's default edition PT.
        var result = await _client.LookupAsync("469844003");

        result.Should().NotBeNull();
        result.Pt.Should().Be("Ampule",
            "LookupAsync returns the International PT because it tries International first " +
            "and 469844003 is an International concept — use LookupDefaultEditionAsync for AU terms");
    }
}
