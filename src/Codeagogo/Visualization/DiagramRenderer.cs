// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace Codeagogo.Visualization;

/// <summary>
/// Renders SNOMED CT concept visualization data as SVG following
/// the SNOMED CT Diagramming Guidelines, ported from the Mac version.
/// </summary>
public static class DiagramRenderer
{
    private const int CharWidth = 5; // ~5px per char at font-size 10

    /// <summary>
    /// Renders a concept visualization as complete HTML with embedded SVG.
    /// </summary>
    public static string RenderHtml(ConceptVisualizationData data, Dictionary<string, bool>? definitionStatusMap = null)
    {
        var svg = RenderSvg(data, definitionStatusMap);
        return WrapInHtml(svg);
    }

    /// <summary>
    /// Renders a concept visualization as an SVG string.
    /// </summary>
    public static string RenderSvg(ConceptVisualizationData data, Dictionary<string, bool>? definitionStatusMap = null)
    {
        definitionStatusMap ??= new Dictionary<string, bool>();
        var elements = new List<string>();
        int currentY = 5;
        int startX = 10;

        var focusConceptId = data.ConceptId;
        bool isFocusDefined = definitionStatusMap.TryGetValue(focusConceptId, out var fd) ? fd : data.SufficientlyDefined;

        // Focus concept colors based on definition status
        var focusColor = isFocusDefined
            ? (fill: "#CCCCFF", stroke: "#6666CC")
            : (fill: "#99CCFF", stroke: "#3366CC");

        // Calculate focus box dimensions
        var focusDisplay = data.PreferredTerm ?? data.ConceptId;
        int focusMaxWidth = 400;
        int idWidth = focusConceptId.Length * CharWidth + 20;
        int termWidth = Math.Min(focusDisplay.Length * CharWidth + 20, focusMaxWidth - 20);
        int focusWidth = Math.Min(Math.Max(Math.Max(idWidth, termWidth), 100) + 20, focusMaxWidth);

        var (focusTextSvg, focusLines) = GenerateConceptText(
            focusConceptId, focusDisplay, startX + 7, currentY + 12, focusWidth - 20);
        int focusBoxHeight = Math.Max(30, 10 + focusLines * 11);
        int focusBoxY = currentY;

        // Draw focus concept box
        elements.Add($"<rect x=\"{startX}\" y=\"{focusBoxY}\" width=\"{focusWidth}\" height=\"{focusBoxHeight}\" rx=\"3\" " +
            $"fill=\"{focusColor.fill}\" stroke=\"{focusColor.stroke}\" stroke-width=\"1.5\"/>");

        if (isFocusDefined)
        {
            elements.Add($"<rect x=\"{startX + 3}\" y=\"{focusBoxY + 3}\" width=\"{focusWidth - 6}\" height=\"{focusBoxHeight - 6}\" rx=\"2\" " +
                $"fill=\"none\" stroke=\"{focusColor.stroke}\" stroke-width=\"1\"/>");
        }

        elements.Add(focusTextSvg);

        // Vertical line from focus box down to symbol
        int symbolCircleX = startX + 15;
        currentY = focusBoxY + focusBoxHeight;

        elements.Add($"<line x1=\"{symbolCircleX}\" y1=\"{currentY}\" x2=\"{symbolCircleX}\" y2=\"{currentY + 20}\" stroke=\"#000\" stroke-width=\"2\"/>");
        currentY += 20;

        // Definition status symbol in circle
        int symbolY = currentY;
        if (data.SufficientlyDefined)
        {
            // Equivalence symbol (≡) for defined concepts - three horizontal lines
            elements.Add($"<circle cx=\"{symbolCircleX}\" cy=\"{symbolY}\" r=\"10\" fill=\"white\" stroke=\"#000\" stroke-width=\"2\"/>");
            elements.Add($"<line x1=\"{symbolCircleX - 6}\" y1=\"{symbolY - 3}\" x2=\"{symbolCircleX + 6}\" y2=\"{symbolY - 3}\" stroke=\"#000\" stroke-width=\"1.5\"/>");
            elements.Add($"<line x1=\"{symbolCircleX - 6}\" y1=\"{symbolY}\" x2=\"{symbolCircleX + 6}\" y2=\"{symbolY}\" stroke=\"#000\" stroke-width=\"1.5\"/>");
            elements.Add($"<line x1=\"{symbolCircleX - 6}\" y1=\"{symbolY + 3}\" x2=\"{symbolCircleX + 6}\" y2=\"{symbolY + 3}\" stroke=\"#000\" stroke-width=\"1.5\"/>");
        }
        else
        {
            // Subsumed by symbol (⊑) for primitive concepts
            elements.Add($"<circle cx=\"{symbolCircleX}\" cy=\"{symbolY}\" r=\"10\" fill=\"white\" stroke=\"#000\" stroke-width=\"2\"/>");
            elements.Add($"<text x=\"{symbolCircleX}\" y=\"{symbolY + 5}\" text-anchor=\"middle\" " +
                $"font-family=\"'Segoe UI', Arial, sans-serif\" font-size=\"16\" font-weight=\"bold\">" +
                "\u2291</text>"); // ⊑ character
        }

        // Horizontal line from symbol to tree junction
        int treeX = symbolCircleX + 35;
        currentY = symbolY;
        elements.Add($"<line x1=\"{symbolCircleX + 10}\" y1=\"{symbolY}\" x2=\"{treeX}\" y2=\"{symbolY}\" stroke=\"#000\" stroke-width=\"2\"/>");

        // Conjunction dot at tree junction
        elements.Add($"<circle cx=\"{treeX}\" cy=\"{symbolY}\" r=\"4\" fill=\"black\" stroke=\"black\" stroke-width=\"1\"/>");

        int treeStartY = symbolY;
        int maxX = startX + focusWidth;
        int parentEndY = symbolY;

        // Draw parent concepts to the right of tree junction
        if (data.Parents.Count > 0)
        {
            int parentSpacing = 10;
            int parentYOffset = 0;
            var parentYPositions = new List<(int y, int height)>();

            for (int i = 0; i < data.Parents.Count; i++)
            {
                var parent = data.Parents[i];
                var parentConceptId = parent.ConceptId;
                bool isParentDefined = definitionStatusMap.TryGetValue(parentConceptId, out var pd) && pd;
                var parentColor = isParentDefined
                    ? (fill: "#CCCCFF", stroke: "#6666CC")
                    : (fill: "#99CCFF", stroke: "#3366CC");

                var parentTerm = parent.Term ?? parent.ConceptId;
                int parentMaxWidth = 500;
                int parentIdWidth = parentConceptId.Length * CharWidth + 20;
                int parentTermWidth = Math.Min(parentTerm.Length * CharWidth + 20, parentMaxWidth - 20);
                int parentWidth = Math.Min(Math.Max(Math.Max(parentIdWidth, parentTermWidth), 100) + 20, parentMaxWidth);

                int parentX = treeX + 35;
                int parentY;

                var (parentTextSvg, parentLines) = GenerateConceptText(
                    parentConceptId, parentTerm, parentX + 8, symbolY - 10, parentWidth - 20);
                int parentBoxHeight = Math.Max(30, 10 + parentLines * 11);

                if (i == 0)
                    parentY = symbolY - (parentBoxHeight / 2);
                else
                    parentY = parentYOffset;

                parentYOffset = parentY + parentBoxHeight + parentSpacing;
                parentEndY = Math.Max(parentEndY, parentY + parentBoxHeight);
                parentYPositions.Add((parentY, parentBoxHeight));

                int arrowY = parentY + (parentBoxHeight / 2);

                // IS-A arrow line from tree to parent
                elements.Add($"<line x1=\"{treeX}\" y1=\"{arrowY}\" x2=\"{parentX}\" y2=\"{arrowY}\" " +
                    $"stroke=\"#000\" stroke-width=\"2\" marker-end=\"url(#isA)\"/>");

                // Parent box
                elements.Add($"<rect x=\"{parentX}\" y=\"{parentY}\" width=\"{parentWidth}\" height=\"{parentBoxHeight}\" rx=\"3\" " +
                    $"fill=\"{parentColor.fill}\" stroke=\"{parentColor.stroke}\" stroke-width=\"1.5\"/>");

                if (isParentDefined)
                {
                    elements.Add($"<rect x=\"{parentX + 3}\" y=\"{parentY + 3}\" width=\"{parentWidth - 6}\" height=\"{parentBoxHeight - 6}\" rx=\"2\" " +
                        $"fill=\"none\" stroke=\"{parentColor.stroke}\" stroke-width=\"1\"/>");
                }

                // Adjust text Y
                int adjustedTextY = parentY + 12;
                var adjustedText = parentTextSvg.Replace($"y=\"{symbolY - 10}\"", $"y=\"{adjustedTextY}\"");
                elements.Add(adjustedText);

                maxX = Math.Max(maxX, parentX + parentWidth);
            }

            // Vertical line through multiple parents
            if (data.Parents.Count > 1)
            {
                int firstMidY = parentYPositions[0].y + parentYPositions[0].height / 2;
                int lastMidY = parentYPositions[^1].y + parentYPositions[^1].height / 2;
                int insertIdx = Math.Max(0, elements.Count - data.Parents.Count * 4);
                elements.Insert(insertIdx,
                    $"<line x1=\"{treeX}\" y1=\"{firstMidY}\" x2=\"{treeX}\" y2=\"{lastMidY}\" stroke=\"#000\" stroke-width=\"2\"/>");
            }
        }

        // Attributes below the symbol/parent area
        currentY = Math.Max(symbolY + 32, parentEndY + 15);
        int treeEndY = currentY;

        // Separate multi-attribute groups from single-attribute groups
        var multiAttrGroups = new List<AttributeGroup>();
        var singleAttrs = new List<ConceptAttribute>();

        foreach (var group in data.AttributeGroups)
        {
            if (group.Attributes.Count > 1)
                multiAttrGroups.Add(group);
            else if (group.Attributes.Count == 1)
                singleAttrs.Add(group.Attributes[0]);
        }

        var allUngrouped = singleAttrs.Concat(data.UngroupedAttributes).ToList();

        // Draw attribute groups and ungrouped attributes
        int circleXAttr = treeX + 15;
        int dotXAttr = circleXAttr + 12;
        int vertLineX = dotXAttr + 15;
        int attrStartX = vertLineX + 15;
        int lastCircleY = currentY;

        // Grouped attributes (multi-attribute groups)
        foreach (var group in multiAttrGroups)
        {
            int groupStartY = currentY + 4;
            lastCircleY = groupStartY;

            // Horizontal line from tree to circle
            elements.Add($"<line x1=\"{treeX}\" y1=\"{groupStartY}\" x2=\"{circleXAttr}\" y2=\"{groupStartY}\" stroke=\"#000\" stroke-width=\"2\"/>");
            // Open circle for role group
            elements.Add($"<circle cx=\"{circleXAttr}\" cy=\"{groupStartY}\" r=\"7\" fill=\"white\" stroke=\"#000\" stroke-width=\"2\"/>");
            // Small filled dot
            elements.Add($"<circle cx=\"{dotXAttr}\" cy=\"{groupStartY}\" r=\"3\" fill=\"#000\"/>");
            // Line from dot to vertical line
            elements.Add($"<line x1=\"{dotXAttr}\" y1=\"{groupStartY}\" x2=\"{vertLineX}\" y2=\"{groupStartY}\" stroke=\"#000\" stroke-width=\"1.5\"/>");

            int attrY = groupStartY;
            int lastAttrY = groupStartY;
            int lastHeight = 0;

            foreach (var attr in group.Attributes)
            {
                // Line from vertical line to attribute
                elements.Add($"<line x1=\"{vertLineX}\" y1=\"{attrY}\" x2=\"{attrStartX}\" y2=\"{attrY}\" stroke=\"#000\" stroke-width=\"1\"/>");

                var (attrMaxX, attrHeight) = DrawAttribute(attr, attrStartX, attrY, elements, definitionStatusMap);
                maxX = Math.Max(maxX, attrMaxX);
                lastAttrY = attrY;
                lastHeight = attrHeight;
                attrY += Math.Max(attrHeight + 5, 26);
            }

            // Vertical line through group attributes
            elements.Add($"<line x1=\"{vertLineX}\" y1=\"{groupStartY}\" x2=\"{vertLineX}\" y2=\"{lastAttrY}\" stroke=\"#000\" stroke-width=\"1.5\"/>");

            currentY = attrY - lastHeight + 30;
        }

        // Ungrouped attributes
        foreach (var attr in allUngrouped)
        {
            int attrY = currentY + 4;
            lastCircleY = attrY;

            elements.Add($"<line x1=\"{treeX}\" y1=\"{attrY}\" x2=\"{circleXAttr}\" y2=\"{attrY}\" stroke=\"#000\" stroke-width=\"2\"/>");
            elements.Add($"<circle cx=\"{circleXAttr}\" cy=\"{attrY}\" r=\"7\" fill=\"white\" stroke=\"#000\" stroke-width=\"2\"/>");
            elements.Add($"<circle cx=\"{dotXAttr}\" cy=\"{attrY}\" r=\"3\" fill=\"#000\"/>");
            elements.Add($"<line x1=\"{dotXAttr}\" y1=\"{attrY}\" x2=\"{attrStartX}\" y2=\"{attrY}\" stroke=\"#000\" stroke-width=\"1\"/>");

            var (attrMaxX, attrHeight) = DrawAttribute(attr, attrStartX, attrY, elements, definitionStatusMap);
            maxX = Math.Max(maxX, attrMaxX);
            currentY = attrY + Math.Max(attrHeight, 28);
        }

        // Main tree vertical line (insert at beginning so it's behind everything)
        if (data.UngroupedAttributes.Count > 0 || data.AttributeGroups.Count > 0)
        {
            elements.Insert(0,
                $"<line x1=\"{treeX}\" y1=\"{treeStartY}\" x2=\"{treeX}\" y2=\"{lastCircleY}\" stroke=\"#000\" stroke-width=\"2\"/>");
        }

        // Build SVG
        int svgWidth = maxX + 20;
        int svgHeight = currentY + 20;

        var sb = new StringBuilder();
        sb.AppendLine($"<svg width=\"{svgWidth}\" height=\"{svgHeight}\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <marker id=\"isA\" markerWidth=\"8\" markerHeight=\"6\" refX=\"8\" refY=\"3\" orient=\"auto\">");
        sb.AppendLine("      <path d=\"M0,0 L8,3 L0,6 Z\" fill=\"white\" stroke=\"#000\" stroke-width=\"1.5\" stroke-linejoin=\"miter\"/>");
        sb.AppendLine("    </marker>");
        sb.AppendLine("  </defs>");
        foreach (var elem in elements)
            sb.AppendLine($"  {elem}");
        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    /// <summary>
    /// Draws a single attribute (type → value) and returns (maxX, height).
    /// </summary>
    private static (int maxX, int height) DrawAttribute(
        ConceptAttribute attribute, int x, int y,
        List<string> elements, Dictionary<string, bool> definitionStatusMap)
    {
        var attrTerm = attribute.Type.Term ?? attribute.Type.ConceptId;
        var attrId = attribute.Type.ConceptId;
        int maxAttrWidth = 300;

        int attrIdWidth = attrId.Length * CharWidth + 20;
        int attrTermWidth = Math.Min(attrTerm.Length * CharWidth + 20, maxAttrWidth - 20);
        int attrWidth = Math.Min(Math.Max(Math.Max(attrIdWidth, attrTermWidth), 90) + 20, maxAttrWidth);

        var (attrTextSvg, attrLines) = GenerateConceptText(attrId, attrTerm, x + 8, y - 12 + 12, attrWidth - 20);
        int attrHeight = Math.Max(24, 10 + attrLines * 11);
        int attrBoxY = y - (attrHeight / 2);

        // Attribute name pill with double border (per SNOMED CT spec) - yellow
        elements.Add($"<rect x=\"{x}\" y=\"{attrBoxY}\" width=\"{attrWidth}\" height=\"{attrHeight}\" " +
            $"rx=\"{attrHeight / 2}\" fill=\"#FFFFCC\" stroke=\"#000\" stroke-width=\"1.5\"/>");
        elements.Add($"<rect x=\"{x + 3}\" y=\"{attrBoxY + 3}\" width=\"{attrWidth - 6}\" height=\"{attrHeight - 6}\" " +
            $"rx=\"{(attrHeight - 6) / 2}\" fill=\"none\" stroke=\"#000\" stroke-width=\"1\"/>");

        // Adjust text Y for centering
        int adjustedTextY = attrBoxY + 12;
        var adjustedAttrText = attrTextSvg.Replace($"y=\"{y - 12 + 12}\"", $"y=\"{adjustedTextY}\"");
        elements.Add(adjustedAttrText);

        // Short line to value
        int lineStartX = x + attrWidth;
        int valueX = lineStartX + 8;
        elements.Add($"<line x1=\"{lineStartX}\" y1=\"{y}\" x2=\"{valueX}\" y2=\"{y}\" stroke=\"#000\" stroke-width=\"1.5\"/>");

        // Value box — for concrete values show just the value, not "concrete" as ID
        var isConcrete = attribute.Value.ConceptId == "concrete";
        var valueTerm = isConcrete
            ? (attribute.Value.Term ?? "")
            : (attribute.Value.Term ?? attribute.Value.ConceptId);
        var valueId = isConcrete ? "" : attribute.Value.ConceptId;
        int maxValueWidth = 400;

        int valueIdWidth = valueId.Length * CharWidth + 20;
        int valueTermWidth = Math.Min(valueTerm.Length * CharWidth + 20, maxValueWidth - 20);
        int valueWidth = Math.Min(Math.Max(Math.Max(valueIdWidth, valueTermWidth), 75) + 20, maxValueWidth);

        var (valueTextSvg, valueLines) = GenerateConceptText(valueId, valueTerm, valueX + 6, y - 12 + 12, valueWidth - 20);
        int valueHeight = Math.Max(24, 10 + valueLines * 11);
        int valueBoxY = y - (valueHeight / 2);

        // Value color: green for concrete values, blue shades for concepts
        (string fill, string stroke) valueColor;
        if (isConcrete)
        {
            valueColor = (fill: "#CCFFCC", stroke: "#339933");
        }
        else
        {
            bool isValueDefined = definitionStatusMap.TryGetValue(valueId, out var vd) && vd;
            valueColor = isValueDefined
                ? (fill: "#CCCCFF", stroke: "#6666CC")
                : (fill: "#99CCFF", stroke: "#3366CC");
        }

        elements.Add($"<rect x=\"{valueX}\" y=\"{valueBoxY}\" width=\"{valueWidth}\" height=\"{valueHeight}\" rx=\"3\" " +
            $"fill=\"{valueColor.fill}\" stroke=\"{valueColor.stroke}\" stroke-width=\"1.5\"/>");

        bool isValueDefined2 = !isConcrete && definitionStatusMap.TryGetValue(valueId, out var vd2) && vd2;
        if (isValueDefined2)
        {
            elements.Add($"<rect x=\"{valueX + 3}\" y=\"{valueBoxY + 3}\" width=\"{valueWidth - 6}\" height=\"{valueHeight - 6}\" rx=\"2\" " +
                $"fill=\"none\" stroke=\"{valueColor.stroke}\" stroke-width=\"1\"/>");
        }

        int adjustedValueTextY = valueBoxY + 12;
        var adjustedValueText = valueTextSvg.Replace($"y=\"{y - 12 + 12}\"", $"y=\"{adjustedValueTextY}\"");
        elements.Add(adjustedValueText);

        int maxHeight = Math.Max(attrHeight, valueHeight);
        return (valueX + valueWidth, maxHeight);
    }

    /// <summary>
    /// Generates SVG text with tspan elements for multi-line display (ID + wrapped term).
    /// </summary>
    private static (string svg, int lines) GenerateConceptText(
        string id, string term, int x, int y, int maxWidth)
    {
        int maxChars = maxWidth / CharWidth;
        var termLines = WrapText(term, maxChars);
        int totalLines = 1 + termLines.Count;

        var sb = new StringBuilder();
        sb.AppendLine($"<text x=\"{x}\" y=\"{y}\" font-family=\"'Segoe UI', Arial, sans-serif\">");

        // First line: concept ID in gray
        sb.AppendLine($"  <tspan x=\"{x}\" dy=\"0\" fill=\"#666\" font-size=\"9\">{EscapeXml(id)}</tspan>");

        // Following lines: term (wrapped)
        foreach (var line in termLines)
        {
            sb.AppendLine($"  <tspan x=\"{x}\" dy=\"11\" font-size=\"10\">{EscapeXml(line)}</tspan>");
        }

        sb.AppendLine("</text>");
        return (sb.ToString(), totalLines);
    }

    /// <summary>
    /// Wraps text into lines that fit within maxChars.
    /// </summary>
    private static List<string> WrapText(string text, int maxChars)
    {
        if (maxChars < 3) maxChars = 3;
        if (text.Length <= maxChars) return new List<string> { text };

        var lines = new List<string>();
        var currentLine = "";
        var words = text.Split(' ');

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            if (testLine.Length <= maxChars)
            {
                currentLine = testLine;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);

                if (word.Length > maxChars)
                {
                    var remaining = word;
                    while (remaining.Length > maxChars)
                    {
                        lines.Add(remaining[..maxChars]);
                        remaining = remaining[maxChars..];
                    }
                    currentLine = remaining;
                }
                else
                {
                    currentLine = word;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    /// <summary>
    /// Wraps SVG diagram in a full HTML page with controls.
    /// </summary>
    private static string WrapInHtml(string diagramSvg)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<style>
  body {{
    font-family: 'Segoe UI', Arial, sans-serif;
    margin: 0;
    padding: 20px;
    background: white;
    font-size: 12px;
  }}
  .diagram-container {{
    overflow: auto;
    border: 1px solid #ddd;
    background: #fafafa;
    display: flex;
    justify-content: flex-start;
    align-items: flex-start;
    min-height: 400px;
    padding: 10px;
  }}
  .diagram-wrapper {{
    transition: transform 0.2s ease;
    transform-origin: top left;
  }}
</style>
</head>
<body>
<div class=""diagram-container"" id=""container"">
  <div class=""diagram-wrapper"" id=""diagram"">
    {diagramSvg}
  </div>
</div>
<script>
  let currentZoom = 1.0;
  const diagram = document.getElementById('diagram');

  function updateZoom() {{
    diagram.style.transform = `scale(${{currentZoom}})`;
  }}

  document.addEventListener('keydown', function(e) {{
    if (e.key === '+' || e.key === '=') {{
      currentZoom = Math.min(currentZoom + 0.2, 3.0);
      updateZoom();
      e.preventDefault();
    }} else if (e.key === '-' || e.key === '_') {{
      currentZoom = Math.max(currentZoom - 0.2, 0.5);
      updateZoom();
      e.preventDefault();
    }} else if (e.key === '0') {{
      currentZoom = 1.0;
      updateZoom();
      e.preventDefault();
    }}
  }});
</script>
</body>
</html>";
    }
}
