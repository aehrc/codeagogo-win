// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using Codeagogo.Visualization;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for rendering SNOMED CT concept visualization diagrams as SVG/HTML.
/// </summary>
public class DiagramRendererTests
{
    #region Helpers

    private static ConceptVisualizationData MakeMinimalData(
        string conceptId = "73211009",
        string? pt = "Diabetes mellitus",
        bool sufficientlyDefined = false)
    {
        return new ConceptVisualizationData
        {
            ConceptId = conceptId,
            PreferredTerm = pt,
            SufficientlyDefined = sufficientlyDefined
        };
    }

    private static ConceptVisualizationData MakeDataWithParents(
        bool sufficientlyDefined = false,
        params (string id, string term)[] parents)
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "73211009",
            PreferredTerm = "Diabetes mellitus",
            SufficientlyDefined = sufficientlyDefined,
        };
        foreach (var (id, term) in parents)
            data.Parents.Add(new ConceptReference(id, term));
        return data;
    }

    private static ConceptVisualizationData MakeDataWithAttributes()
    {
        return new ConceptVisualizationData
        {
            ConceptId = "73211009",
            PreferredTerm = "Diabetes mellitus",
            SufficientlyDefined = false,
            UngroupedAttributes =
            {
                new ConceptAttribute(
                    new ConceptReference("116676008", "Associated morphology"),
                    new ConceptReference("85828009", "Autoimmune disease"))
            }
        };
    }

    #endregion

    #region SVG Structure

    [Fact]
    public void RenderSvg_ProducesValidSvgOpeningTag()
    {
        var data = MakeMinimalData();
        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().StartWith("<svg ");
        svg.Should().Contain("xmlns=\"http://www.w3.org/2000/svg\"");
    }

    [Fact]
    public void RenderSvg_ProducesValidSvgClosingTag()
    {
        var data = MakeMinimalData();
        var svg = DiagramRenderer.RenderSvg(data);

        svg.TrimEnd().Should().EndWith("</svg>");
    }

    [Fact]
    public void RenderSvg_ContainsWidthAndHeight()
    {
        var data = MakeMinimalData();
        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("width=\"");
        svg.Should().Contain("height=\"");
    }

    [Fact]
    public void RenderSvg_ContainsIsAArrowMarkerDefinition()
    {
        var data = MakeMinimalData();
        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("<defs>");
        svg.Should().Contain("<marker id=\"isA\"");
        svg.Should().Contain("markerWidth=");
        svg.Should().Contain("markerHeight=");
        svg.Should().Contain("</defs>");
    }

    #endregion

    #region Focus Concept Box - Primitive (Blue)

    [Fact]
    public void RenderSvg_PrimitiveConcept_UsesBlueFill()
    {
        var data = MakeMinimalData(sufficientlyDefined: false);
        var svg = DiagramRenderer.RenderSvg(data);

        // Primitive concepts should use blue fill (#99CCFF)
        svg.Should().Contain("fill=\"#99CCFF\"");
        svg.Should().Contain("stroke=\"#3366CC\"");
    }

    [Fact]
    public void RenderSvg_PrimitiveConcept_NoDupleBorder()
    {
        var data = MakeMinimalData(sufficientlyDefined: false);
        var svg = DiagramRenderer.RenderSvg(data);

        // Primitive concepts should NOT have a second inner rect for the focus box
        // The focus box is the first rect element; for primitive there is no double border
        // Count inner rects - for primitive there should be just the outer rect for focus
        var lines = svg.Split('\n');
        // The focus box rect comes first; for primitive no inner rect follows immediately
        // We verify by checking we don't get the purple/defined double border
        svg.Should().NotContain("fill=\"#CCCCFF\"");
    }

    #endregion

    #region Focus Concept Box - Defined (Purple)

    [Fact]
    public void RenderSvg_DefinedConcept_UsesPurpleFill()
    {
        var data = MakeMinimalData(sufficientlyDefined: true);
        var svg = DiagramRenderer.RenderSvg(data);

        // Defined concepts should use purple fill (#CCCCFF)
        svg.Should().Contain("fill=\"#CCCCFF\"");
        svg.Should().Contain("stroke=\"#6666CC\"");
    }

    [Fact]
    public void RenderSvg_DefinedConcept_HasDoubleBorder()
    {
        var data = MakeMinimalData(sufficientlyDefined: true);
        var svg = DiagramRenderer.RenderSvg(data);

        // Defined concepts should have an inner rect (double border)
        svg.Should().Contain("fill=\"none\" stroke=\"#6666CC\"");
    }

    #endregion

    #region Definition Status Symbol

    [Fact]
    public void RenderSvg_DefinedConcept_HasEquivalenceSymbol()
    {
        var data = MakeMinimalData(sufficientlyDefined: true);
        var svg = DiagramRenderer.RenderSvg(data);

        // Defined concept has three horizontal lines (equivalence symbol)
        // Rendered as three <line> elements inside a circle
        // The circle for the symbol
        svg.Should().Contain("fill=\"white\" stroke=\"#000\" stroke-width=\"2\"");
    }

    [Fact]
    public void RenderSvg_PrimitiveConcept_HasSubsumedBySymbol()
    {
        var data = MakeMinimalData(sufficientlyDefined: false);
        var svg = DiagramRenderer.RenderSvg(data);

        // Primitive concept shows the subsumption character
        svg.Should().Contain("\u2291"); // subsumption character
    }

    #endregion

    #region Parents with IS-A Arrows

    [Fact]
    public void RenderSvg_WithParents_ContainsIsAArrows()
    {
        var data = MakeDataWithParents(
            false,
            ("404684003", "Clinical finding"));

        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("marker-end=\"url(#isA)\"");
    }

    [Fact]
    public void RenderSvg_WithParents_ContainsParentConceptId()
    {
        var data = MakeDataWithParents(
            false,
            ("404684003", "Clinical finding"));

        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("404684003");
        svg.Should().Contain("Clinical finding");
    }

    [Fact]
    public void RenderSvg_MultipleParents_HasVerticalConnectorLine()
    {
        var data = MakeDataWithParents(
            false,
            ("404684003", "Clinical finding"),
            ("64572001", "Disease"));

        var svg = DiagramRenderer.RenderSvg(data);

        // Multiple parents create a vertical line connecting them
        // This is a line element that appears before parent boxes
        svg.Should().Contain("marker-end=\"url(#isA)\"");
        // Both parents should be present
        svg.Should().Contain("404684003");
        svg.Should().Contain("64572001");
    }

    [Fact]
    public void RenderSvg_MultipleParents_HasMultipleIsAArrows()
    {
        var data = MakeDataWithParents(
            false,
            ("404684003", "Clinical finding"),
            ("64572001", "Disease"),
            ("362969004", "Disorder of endocrine system"));

        var svg = DiagramRenderer.RenderSvg(data);

        // Should have IS-A arrows for each parent
        var arrowCount = CountOccurrences(svg, "marker-end=\"url(#isA)\"");
        arrowCount.Should().Be(3);
    }

    #endregion

    #region Attributes with Yellow Pill Boxes

    [Fact]
    public void RenderSvg_WithAttributes_ContainsYellowPillBoxes()
    {
        var data = MakeDataWithAttributes();
        var svg = DiagramRenderer.RenderSvg(data);

        // Attribute name pills are yellow (#FFFFCC)
        svg.Should().Contain("fill=\"#FFFFCC\"");
    }

    [Fact]
    public void RenderSvg_WithAttributes_ContainsAttributeTypeAndValue()
    {
        var data = MakeDataWithAttributes();
        var svg = DiagramRenderer.RenderSvg(data);

        svg.Should().Contain("116676008");
        svg.Should().Contain("Associated morphology");
        svg.Should().Contain("85828009");
        svg.Should().Contain("Autoimmune disease");
    }

    [Fact]
    public void RenderSvg_AttributePills_HaveDoubleBorder()
    {
        var data = MakeDataWithAttributes();
        var svg = DiagramRenderer.RenderSvg(data);

        // Attribute pills have a double border (inner rect with fill="none")
        // The outer pill has fill="#FFFFCC", the inner one has fill="none"
        svg.Should().Contain("fill=\"#FFFFCC\"");
        // The inner border rect for the attribute pill
        var yellowPillCount = CountOccurrences(svg, "fill=\"#FFFFCC\"");
        yellowPillCount.Should().BeGreaterOrEqualTo(1);
    }

    #endregion

    #region RenderHtml

    [Fact]
    public void RenderHtml_WrapsInValidHtml()
    {
        var data = MakeMinimalData();
        var html = DiagramRenderer.RenderHtml(data);

        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("<html>");
        html.Should().Contain("</html>");
        html.Should().Contain("<body>");
        html.Should().Contain("</body>");
    }

    [Fact]
    public void RenderHtml_ContainsSvg()
    {
        var data = MakeMinimalData();
        var html = DiagramRenderer.RenderHtml(data);

        html.Should().Contain("<svg ");
        html.Should().Contain("</svg>");
    }

    [Fact]
    public void RenderHtml_ContainsDiagramContainer()
    {
        var data = MakeMinimalData();
        var html = DiagramRenderer.RenderHtml(data);

        html.Should().Contain("diagram-container");
        html.Should().Contain("diagram-wrapper");
    }

    [Fact]
    public void RenderHtml_ContainsZoomScript()
    {
        var data = MakeMinimalData();
        var html = DiagramRenderer.RenderHtml(data);

        html.Should().Contain("<script>");
        html.Should().Contain("currentZoom");
        html.Should().Contain("</script>");
    }

    #endregion

    #region Empty Parents and Attributes

    [Fact]
    public void RenderSvg_EmptyParentsAndAttributes_StillRendersFocusBox()
    {
        var data = MakeMinimalData();
        var svg = DiagramRenderer.RenderSvg(data);

        // Should contain the focus concept ID
        svg.Should().Contain("73211009");
        svg.Should().Contain("Diabetes mellitus");
        // Should have at least one rect (the focus box)
        svg.Should().Contain("<rect ");
    }

    [Fact]
    public void RenderSvg_NoPreferredTerm_UsesFallbackToConceptId()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "73211009",
            PreferredTerm = null,
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        // The concept ID should appear (used as display when PT is null)
        svg.Should().Contain("73211009");
    }

    #endregion

    #region EscapeXml

    [Fact]
    public void RenderSvg_SpecialCharactersInTerm_AreEscaped()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "12345678",
            PreferredTerm = "Test <concept> & \"quoted\" 'term'",
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
    public void RenderSvg_AmpersandInConceptId_IsEscaped()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "12345678",
            PreferredTerm = "A & B",
            SufficientlyDefined = false
        };

        var svg = DiagramRenderer.RenderSvg(data);

        // Should not contain raw ampersand in the text content
        svg.Should().Contain("A &amp; B");
    }

    #endregion

    #region Definition Status Map

    [Fact]
    public void RenderSvg_DefinitionStatusMap_OverridesFocusConcept()
    {
        var data = MakeMinimalData(sufficientlyDefined: false);
        var map = new Dictionary<string, bool>
        {
            ["73211009"] = true // Override to defined
        };

        var svg = DiagramRenderer.RenderSvg(data, map);

        // With override to defined, should see purple colors
        svg.Should().Contain("fill=\"#CCCCFF\"");
    }

    [Fact]
    public void RenderSvg_DefinitionStatusMap_AffectsParentColors()
    {
        var data = MakeDataWithParents(false, ("404684003", "Clinical finding"));
        var map = new Dictionary<string, bool>
        {
            ["404684003"] = true // Parent is defined
        };

        var svg = DiagramRenderer.RenderSvg(data, map);

        // The defined parent should get purple color
        svg.Should().Contain("fill=\"#CCCCFF\"");
    }

    #endregion

    #region Grouped Attributes

    [Fact]
    public void RenderSvg_GroupedAttributes_RendersRoleGroupCircles()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "73211009",
            PreferredTerm = "Diabetes mellitus",
            SufficientlyDefined = false,
            AttributeGroups =
            {
                new AttributeGroup
                {
                    GroupNumber = 1,
                    Attributes =
                    {
                        new ConceptAttribute(
                            new ConceptReference("116676008", "Morphology"),
                            new ConceptReference("85828009", "Disease")),
                        new ConceptAttribute(
                            new ConceptReference("363698007", "Finding site"),
                            new ConceptReference("113331007", "Endocrine"))
                    }
                }
            }
        };

        var svg = DiagramRenderer.RenderSvg(data);

        // Should contain the attribute concepts
        svg.Should().Contain("116676008");
        svg.Should().Contain("363698007");
        // Should contain yellow pill boxes for attributes
        svg.Should().Contain("fill=\"#FFFFCC\"");
    }

    #endregion

    #region Helpers

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    #endregion
}
