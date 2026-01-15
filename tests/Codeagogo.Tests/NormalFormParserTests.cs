// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using Codeagogo.Visualization;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Comprehensive tests for parsing SNOMED CT normal form (compositional grammar) expressions.
/// Ported from the Mac SNOMEDExpressionParserTests with additions for edge cases
/// that previously caused hangs (Australian CTPP normal forms).
/// </summary>
public class NormalFormParserTests
{
    #region Empty/Null Input

    [Fact]
    public void Parse_NullInput_ReturnsEmptyResult()
    {
        var result = NormalFormParser.Parse(null!);
        result.Should().NotBeNull();
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyResult()
    {
        var result = NormalFormParser.Parse("");
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyResult()
    {
        var result = NormalFormParser.Parse("   \t\n  ");
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    #endregion

    #region Simple Expressions (No Refinement)

    [Fact]
    public void Parse_SimpleConcept_NoRefinement_ReturnsEmpty()
    {
        var result = NormalFormParser.Parse("73211009");
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ConceptWithTerm_NoRefinement_ReturnsEmpty()
    {
        var result = NormalFormParser.Parse("73211009 |Diabetes mellitus|");
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_MultipleFocusConcepts_NoRefinement_ReturnsEmpty()
    {
        var result = NormalFormParser.Parse("73211009 + 385804009");
        result.UngroupedAttributes.Should().BeEmpty();
    }

    #endregion

    #region Definition Status

    [Fact]
    public void Parse_DefinedStatus_Parses()
    {
        var result = NormalFormParser.Parse("=== 73211009:{116676008=49601007}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_PrimitiveStatus_Parses()
    {
        var result = NormalFormParser.Parse("<<< 73211009:{116676008=49601007}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_NoStatus_DefaultsPrimitive_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=49601007}");
        result.Groups.Should().HaveCount(1);
    }

    #endregion

    #region Ungrouped Attributes

    [Fact]
    public void Parse_SingleUngroupedAttribute_Parses()
    {
        var result = NormalFormParser.Parse("73211009:116676008=49601007");
        result.UngroupedAttributes.Should().HaveCount(1);
        result.UngroupedAttributes[0].Type.ConceptId.Should().Be("116676008");
        result.UngroupedAttributes[0].Value.ConceptId.Should().Be("49601007");
    }

    [Fact]
    public void Parse_UngroupedAttributeWithTerms_ExtractsTerms()
    {
        var result = NormalFormParser.Parse(
            "73211009 |Diabetes mellitus|:116676008 |Associated morphology|=49601007 |Disorder|");
        result.UngroupedAttributes.Should().HaveCount(1);
        result.UngroupedAttributes[0].Type.Term.Should().Be("Associated morphology");
        result.UngroupedAttributes[0].Value.Term.Should().Be("Disorder");
    }

    [Fact]
    public void Parse_MultipleUngroupedAttributes_ParsesAll()
    {
        var result = NormalFormParser.Parse(
            "73211009:116676008=49601007,363698007=113331007");
        result.UngroupedAttributes.Should().HaveCount(2);
        result.UngroupedAttributes[0].Type.ConceptId.Should().Be("116676008");
        result.UngroupedAttributes[1].Type.ConceptId.Should().Be("363698007");
    }

    #endregion

    #region Grouped Attributes

    [Fact]
    public void Parse_SingleGroup_SingleAttribute_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=49601007}");
        result.Groups.Should().HaveCount(1);
        result.Groups[0].Attributes.Should().HaveCount(1);
        result.Groups[0].Attributes[0].Type.ConceptId.Should().Be("116676008");
    }

    [Fact]
    public void Parse_SingleGroup_MultipleAttributes_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009:{116676008=49601007,363698007=113331007}");
        result.Groups.Should().HaveCount(1);
        result.Groups[0].Attributes.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_MultipleGroups_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009:{116676008=49601007},{363698007=113331007}");
        result.Groups.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_MixedUngroupedAndGrouped_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009:116676008=49601007,{363698007=113331007}");
        result.UngroupedAttributes.Should().HaveCount(1);
        result.Groups.Should().HaveCount(1);
    }

    #endregion

    #region Concrete Values

    [Fact]
    public void Parse_ConcreteInteger_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=#500}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_ConcreteDecimal_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=#37.5}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_ConcreteNegativeNumber_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=#-10.5}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_ConcreteStringWithHash_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=#\"some text\"}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_QuotedStringWithoutHash_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=\"some value\"}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_ConcreteZero_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{1142142004=#0}");
        result.Groups.Should().HaveCount(1);
    }

    #endregion

    #region Nested Expressions

    [Fact]
    public void Parse_NestedExpression_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009:{116676008=(49601007:363698007=113331007)}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_DeeplyNestedExpression_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009:{116676008=(49601007:363698007=(113331007:116676008=49601007))}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_NestedExpressionWithConcreteValues_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009:{774160008=(23340011000036101:411116001=385268001),1142142004=#56.0}");
        result.Groups.Should().HaveCount(1);
    }

    #endregion

    #region Focus Concepts with Plus Separator

    [Fact]
    public void Parse_FocusConceptsWithPlus_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009+385804009:{116676008=49601007}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_FocusConceptsWithPlusAndTerms_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009 |Diabetes|+385804009 |Diabetic care|:{116676008=49601007}");
        result.Groups.Should().HaveCount(1);
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void Parse_ExtraWhitespace_Parses()
    {
        var result = NormalFormParser.Parse(
            "   73211009   :   116676008   =   49601007   ");
        result.UngroupedAttributes.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_NewlinesAndTabs_Parses()
    {
        var result = NormalFormParser.Parse(
            "73211009\n:\n\t116676008\n\t=\n\t49601007");
        result.UngroupedAttributes.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_NoWhitespace_Parses()
    {
        var result = NormalFormParser.Parse("73211009:116676008=49601007");
        result.UngroupedAttributes.Should().HaveCount(1);
    }

    #endregion

    #region Realistic Normal Forms

    [Fact]
    public void Parse_RealisticDiabetes_Parses()
    {
        var input = "=== 73211009 |Diabetes mellitus| : " +
            "116676008 |Associated morphology| = 49601007 |Disorder of structure|, " +
            "{ 363698007 |Finding site| = 113331007 |Endocrine system| }";
        var result = NormalFormParser.Parse(input);

        result.UngroupedAttributes.Should().HaveCount(1);
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_AustralianCTPP_DoesNotHang()
    {
        // Real Australian CTPP normal form that caused the old parser to hang
        var normalForm = """
            === 869461000168109|Atomerra (atomoxetine 60 mg) capsule, 56 capsules|:{1142142004|Has pack size|=#56.0,774160008|Contains clinical drug|=(23340011000036101|Atomoxetine 60 mg capsule|:411116001|Has manufactured dose form|=(385268001|Oral dose form|:{736475003|Has dose form release characteristic|=736849007|Conventional release|},{736474004|Has dose form intended site|=738956005|Oral|},{736473005|Has dose form transformation|=761954006|No transformation|},{736472000|Has dose form administration method|=738995006|Swallow|},{736476002|Has basic dose form|=(385049006|Capsule|:{736518005|Has state of matter|=736678006|Solid|})}){127489000|Has active ingredient|=(407037005|Atomoxetine|:{726542003|Has disposition|=734572000|Norepinephrine reuptake inhibitor|}),999000051000168108|Has total quantity unit|=258684004|milligram|,999000041000168106|Has total quantity value|=#60.0,732943007|Has basis of strength substance|=(407037005|Atomoxetine|:{726542003|Has disposition|=734572000|Norepinephrine reuptake inhibitor|})},{774158006|Has product name|=868501000168100|Atomerra|},{999000001000168109|Has other identifying information|="None"},{1142140007|Count of active ingredient|=#1}),999000131000168101|Count of contained component ingredient|=#1,774163005|Has pack size unit|=732935002|Unit of presentation|},{774158006|Has product name|=868501000168100|Atomerra|},{30465011000036106|Has container type|=287011000036106|Blister pack|},{1142143009|Count of clinical drug type|=#1}
            """;

        var result = NormalFormParser.Parse(normalForm);
        result.Should().NotBeNull();
        result.Groups.Should().NotBeEmpty("CTPP normal form should have attribute groups");
    }

    [Fact]
    public void Parse_ComplexWithMultipleNestedAndConcrete_Parses()
    {
        // Simplified but representative complex normal form
        var input = "=== 100000000:{200000000=#56.0,300000000=(400000000:500000000=(600000000:{700000000=800000000})){900000000=#60.0},1000000000=\"text\"}";
        var result = NormalFormParser.Parse(input);
        result.Groups.Should().NotBeEmpty();
    }

    #endregion

    #region Nested Expression Values

    [Fact]
    public void Parse_NestedExpression_ReturnsFocusConcept()
    {
        // Nested expressions return the focus concept — the caller replaces
        // with the pre-coordinated concept ID from FHIR properties
        var normalForm = "100000000:{200000000=(300000000|Some concept|:400000000=500000000)}";
        var result = NormalFormParser.Parse(normalForm);

        result.Groups.Should().HaveCount(1);
        var attr = result.Groups[0].Attributes[0];
        attr.Value.ConceptId.Should().Be("300000000");
        attr.Value.Term.Should().Be("Some concept");
    }

    [Fact]
    public void Parse_NestedWithNumeratorDenominator_ReturnsFocusConcept()
    {
        // Even with numerator/denominator pattern, returns focus concept
        // (pre-coordinated replacement happens at the visualization layer)
        var normalForm = "100000000:{200000000=(415777001|Unit|:{700000091000036104=300000000},{700000071000036103=400000000})}";
        var result = NormalFormParser.Parse(normalForm);

        result.Groups.Should().HaveCount(1);
        var attr = result.Groups[0].Attributes[0];
        attr.Value.ConceptId.Should().Be("415777001");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Parse_EmptyGroup_Parses()
    {
        // Edge case: empty braces
        var result = NormalFormParser.Parse("73211009:{}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_TrailingComma_Parses()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=49601007,}");
        result.Groups.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_NonDigitAfterColon_ReturnsGracefully()
    {
        // Malformed input — should not crash
        var result = NormalFormParser.Parse("73211009:abc=def");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_MissingEquals_ReturnsGracefully()
    {
        // Malformed: no equals sign between type and value
        var result = NormalFormParser.Parse("73211009:116676008 49601007");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_UnclosedPipe_ReturnsGracefully()
    {
        var result = NormalFormParser.Parse("73211009 |Diabetes mellitus:{116676008=49601007}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_UnclosedBrace_ReturnsGracefully()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=49601007");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_UnclosedParen_ReturnsGracefully()
    {
        var result = NormalFormParser.Parse("73211009:{116676008=(49601007:363698007=113331007}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_OnlyColon_ReturnsGracefully()
    {
        var result = NormalFormParser.Parse("73211009:");
        result.Should().NotBeNull();
    }

    #endregion

    #region Security / Performance Limits

    [Fact]
    public void Parse_DeeplyNested50Levels_Completes()
    {
        var inner = "100000000";
        for (int i = 0; i < 50; i++)
            inner = $"({inner}:200000000=300000000)";
        var normalForm = $"100000000:{{{400000000}={inner}}}";

        var result = NormalFormParser.Parse(normalForm);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Parse_100Groups_Parses()
    {
        var groups = string.Join(",", Enumerable.Range(1, 100).Select(i =>
            $"{{{100000000 + i}=200000000}}"));
        var normalForm = $"123456789:{groups}";

        var result = NormalFormParser.Parse(normalForm);
        result.Groups.Should().HaveCount(100);
    }

    [Fact]
    public void Parse_InputExceedingMaxSize_ReturnsEmpty()
    {
        var huge = new string('0', 200_000);
        var result = NormalFormParser.Parse(huge);
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void Parse_InputAtMaxSize_DoesNotCrash()
    {
        var atLimit = new string('0', 100_000);
        var act = () => NormalFormParser.Parse(atLimit);
        act.Should().NotThrow();
    }

    #endregion

    #region NormalFormResult Default State

    [Fact]
    public void NormalFormResult_DefaultState_HasEmptyLists()
    {
        var result = new NormalFormResult();
        result.UngroupedAttributes.Should().NotBeNull();
        result.UngroupedAttributes.Should().BeEmpty();
        result.Groups.Should().NotBeNull();
        result.Groups.Should().BeEmpty();
    }

    #endregion

    #region ConceptReference and ConceptAttribute Records

    [Fact]
    public void ConceptReference_StoresIdAndTerm()
    {
        var r = new ConceptReference("73211009", "Diabetes mellitus");
        r.ConceptId.Should().Be("73211009");
        r.Term.Should().Be("Diabetes mellitus");
    }

    [Fact]
    public void ConceptReference_NullTerm()
    {
        var r = new ConceptReference("73211009", null);
        r.Term.Should().BeNull();
    }

    [Fact]
    public void ConceptAttribute_StoresTypeAndValue()
    {
        var type = new ConceptReference("116676008", "Associated morphology");
        var value = new ConceptReference("49601007", "Disorder");
        var attr = new ConceptAttribute(type, value);

        attr.Type.ConceptId.Should().Be("116676008");
        attr.Value.ConceptId.Should().Be("49601007");
    }

    [Fact]
    public void AttributeGroup_StoresAttributes()
    {
        var group = new AttributeGroup { GroupNumber = 1 };
        group.Attributes.Add(new ConceptAttribute(
            new ConceptReference("116676008", null),
            new ConceptReference("49601007", null)));
        group.Attributes.Add(new ConceptAttribute(
            new ConceptReference("363698007", null),
            new ConceptReference("113331007", null)));

        group.Attributes.Should().HaveCount(2);
        group.GroupNumber.Should().Be(1);
    }

    #endregion
}

/// <summary>
/// Tests for diagram display term resolution — verifies the complete pipeline
/// from normal form parsing through property replacement to enrichment.
/// </summary>
public class DiagramDisplayTermTests
{
    /// <summary>
    /// The exact normal form from concept 37933011000036106 (Midazolam 5 mg/mL injection, ampoule).
    /// This is the real data that exposed all three display term bugs.
    /// </summary>
    private const string MidazolamNormalForm = "=== 389105002|Product containing midazolam in parenteral dose form|:411116001|Has manufactured dose form|=(129011000036109|Injection dose form|:{736474004|Has dose form intended site|=738984000|Parenteral|}){999000051000168108|Has total quantity unit|=258684004|milligram|,999000041000168106|Has total quantity value|=#5.0,999000021000168100|Has concentration strength value|=#5.0,732943007|Has basis of strength substance|=373476007|Midazolam|,999000031000168102|Has concentration strength unit|=(415777001|Unit of mass concentration|:{700000071000036103|Has denominator units|=258773002|Milliliter|},{700000091000036104|Has numerator units|=258684004|milligram|}),127489000|Has active ingredient|=373476007|Midazolam|},{1142142004|Has pack size|=#1.0,774163005|Has pack size unit|=258773002|Milliliter|},{30465011000036106|Has container type|=469844003|Ampule|}";

    /// <summary>
    /// Relationship properties from the FHIR $lookup response for this concept.
    /// These map attribute type IDs to pre-coordinated value concept IDs.
    /// </summary>
    private static Dictionary<string, string> MidazolamProperties => new()
    {
        ["127489000"] = "373476007",           // Has active ingredient = Midazolam
        ["411116001"] = "129011000036109",     // Has manufactured dose form = Injection dose form
        ["732943007"] = "373476007",           // Has BoSS = Midazolam
        ["774163005"] = "258773002",           // Has pack size unit = mL
        ["30465011000036106"] = "469844003",   // Has container type = Ampule/Ampoule
        ["999000031000168102"] = "258798001",  // Has concentration strength unit = mg/mL (NOT 415777001!)
        ["999000051000168108"] = "258684004",  // Has total quantity unit = mg
    };

    [Fact]
    public void Parse_MidazolamNormalForm_Parses()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        result.Groups.Should().NotBeEmpty("should have attribute groups");
        result.UngroupedAttributes.Should().NotBeEmpty("should have ungrouped attributes");
    }

    [Fact]
    public void Parse_MidazolamNormalForm_ConcentrationStrengthUnit_IsFocusConcept()
    {
        // Before replacement, the concentration strength unit value is the focus concept
        // of the nested expression (415777001 "Unit of mass concentration")
        var result = NormalFormParser.Parse(MidazolamNormalForm);

        var allAttrs = result.Groups.SelectMany(g => g.Attributes)
            .Concat(result.UngroupedAttributes);

        var concStrengthUnit = allAttrs.FirstOrDefault(a => a.Type.ConceptId == "999000031000168102");
        concStrengthUnit.Should().NotBeNull("should find 'Has concentration strength unit' attribute");
        concStrengthUnit!.Value.ConceptId.Should().Be("415777001",
            "normal form decomposes mg/mL to its parent concept 415777001");
    }

    [Fact]
    public void ReplaceWithPreCoordinatedValues_FixesConcentrationStrengthUnit()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        var vizData = new ConceptVisualizationData
        {
            ConceptId = "37933011000036106",
            PreferredTerm = "Midazolam 5 mg/mL injection, ampoule"
        };
        vizData.UngroupedAttributes.AddRange(result.UngroupedAttributes);
        vizData.AttributeGroups.AddRange(result.Groups);

        // Apply pre-coordinated replacement
        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, MidazolamProperties);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes)
            .Concat(vizData.UngroupedAttributes);

        var concStrengthUnit = allAttrs.FirstOrDefault(a => a.Type.ConceptId == "999000031000168102");
        concStrengthUnit.Should().NotBeNull();
        concStrengthUnit!.Value.ConceptId.Should().Be("258798001",
            "should be replaced with pre-coordinated concept mg/mL");
    }

    [Fact]
    public void ReplaceWithPreCoordinatedValues_PreservesConcreteValues()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        var vizData = new ConceptVisualizationData { ConceptId = "37933011000036106" };
        vizData.UngroupedAttributes.AddRange(result.UngroupedAttributes);
        vizData.AttributeGroups.AddRange(result.Groups);

        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, MidazolamProperties);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes)
            .Concat(vizData.UngroupedAttributes);

        // Concrete values should NOT be replaced
        var concreteAttrs = allAttrs.Where(a => a.Value.ConceptId == "concrete").ToList();
        concreteAttrs.Should().NotBeEmpty("concrete values should be preserved");
        foreach (var attr in concreteAttrs)
        {
            attr.Value.Term.Should().NotBeNullOrEmpty("concrete values should have their numeric text");
        }
    }

    [Fact]
    public void ReplaceWithPreCoordinatedValues_ReplacesContainerType()
    {
        var result = NormalFormParser.Parse(MidazolamNormalForm);
        var vizData = new ConceptVisualizationData { ConceptId = "37933011000036106" };
        vizData.UngroupedAttributes.AddRange(result.UngroupedAttributes);
        vizData.AttributeGroups.AddRange(result.Groups);

        vizData = VisualizationWindow.ReplaceWithPreCoordinatedValues(vizData, MidazolamProperties);

        var allAttrs = vizData.AttributeGroups.SelectMany(g => g.Attributes)
            .Concat(vizData.UngroupedAttributes);

        // Container type: the normal form has 469844003 and the property also has 469844003
        // so it should NOT be replaced (same value)
        var containerType = allAttrs.FirstOrDefault(a => a.Type.ConceptId == "30465011000036106");
        containerType.Should().NotBeNull();
        containerType!.Value.ConceptId.Should().Be("469844003");
    }
}
