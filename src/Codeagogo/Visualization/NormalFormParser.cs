// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo.Visualization;

/// <summary>
/// Parses SNOMED CT normal form (compositional grammar) expressions
/// to extract defining relationships for visualization.
/// </summary>
/// <remarks>
/// Handles the full SNOMED CT compositional grammar including:
/// - Definition status (=== for defined, &lt;&lt;&lt; for primitive)
/// - Focus concepts (separated by +)
/// - Grouped and ungrouped attributes
/// - Concrete values (#number, "string")
/// - Nested expressions in parentheses
/// - Depth and input size limits for safety
/// </remarks>
public static class NormalFormParser
{
    private const int MaxInputSize = 100_000;
    private const int MaxDepth = 100;

    /// <summary>
    /// Parses a SNOMED CT normal form expression into visualization data.
    /// </summary>
    /// <param name="normalForm">The normal form expression string</param>
    /// <returns>Parsed attributes (ungrouped and grouped)</returns>
    public static NormalFormResult Parse(string normalForm)
    {
        var result = new NormalFormResult();
        if (string.IsNullOrWhiteSpace(normalForm)) return result;
        if (normalForm.Length > MaxInputSize) return result;

        try
        {
            var parser = new ExpressionParser(normalForm);
            var expr = parser.Parse();

            if (expr.Refinement != null)
            {
                result.UngroupedAttributes.AddRange(expr.Refinement.UngroupedAttributes);
                result.Groups.AddRange(expr.Refinement.Groups);
            }
        }
        catch (Exception ex)
        {
            Log.Debug($"NormalFormParser: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Internal recursive descent parser for SNOMED CT compositional grammar.
    /// </summary>
    private ref struct ExpressionParser
    {
        private readonly ReadOnlySpan<char> _input;
        private int _pos;
        private int _depth;

        public ExpressionParser(string input)
        {
            _input = input.AsSpan().Trim();
            _pos = 0;
            _depth = 0;
        }

        public ParsedExpression Parse()
        {
            SkipWhitespace();

            // Skip definition status prefix (=== or <<<) if present
            if (!TryConsume("==="))
                TryConsume("<<<");

            SkipWhitespace();

            // Parse focus concepts (before the colon)
            var focusConcepts = ParseFocusConcepts();

            SkipWhitespace();

            // Parse refinement if present
            ParsedRefinement? refinement = null;
            if (TryConsume(":"))
            {
                SkipWhitespace();
                refinement = ParseRefinement();
            }

            return new ParsedExpression(focusConcepts, refinement);
        }

        private List<ConceptReference> ParseFocusConcepts()
        {
            var concepts = new List<ConceptReference>();
            var first = TryParseConceptReference();
            if (first != null)
                concepts.Add(first);

            SkipWhitespace();
            while (TryConsume("+"))
            {
                SkipWhitespace();
                var next = TryParseConceptReference();
                if (next != null)
                    concepts.Add(next);
                SkipWhitespace();
            }

            return concepts;
        }

        private ParsedRefinement ParseRefinement()
        {
            var groups = new List<AttributeGroup>();
            var ungrouped = new List<ConceptAttribute>();
            int groupNumber = 1;

            while (_pos < _input.Length)
            {
                SkipWhitespace();
                if (_pos >= _input.Length) break;

                // Check for end conditions (closing paren or brace from parent context)
                if (_input[_pos] == ')' || _input[_pos] == '}') break;

                if (_input[_pos] == '{')
                {
                    // Grouped attributes
                    _pos++; // skip '{'
                    SkipWhitespace();
                    var attrs = ParseAttributeList('}');
                    SkipWhitespace();
                    if (_pos < _input.Length && _input[_pos] == '}')
                        _pos++;
                    if (attrs.Count > 0)
                        groups.Add(new AttributeGroup { GroupNumber = groupNumber++, Attributes = { } });

                    // Add attributes to the group we just created
                    if (attrs.Count > 0)
                    {
                        groups[^1].Attributes.AddRange(attrs);
                    }
                }
                else if (char.IsDigit(_input[_pos]))
                {
                    // Ungrouped attribute
                    var attr = TryParseAttribute();
                    if (attr != null)
                        ungrouped.Add(attr);
                    else
                        break; // Can't parse further
                }
                else
                {
                    break; // Unknown character
                }

                SkipWhitespace();
                TryConsume(","); // optional comma separator
            }

            return new ParsedRefinement(ungrouped, groups);
        }

        private List<ConceptAttribute> ParseAttributeList(char terminator)
        {
            var attrs = new List<ConceptAttribute>();

            SkipWhitespace();
            if (_pos < _input.Length && _input[_pos] == terminator)
                return attrs;

            var first = TryParseAttribute();
            if (first != null)
                attrs.Add(first);

            SkipWhitespace();
            while (TryConsume(","))
            {
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == terminator)
                    break;
                var next = TryParseAttribute();
                if (next != null)
                    attrs.Add(next);
                SkipWhitespace();
            }

            return attrs;
        }

        private ConceptAttribute? TryParseAttribute()
        {
            var type = TryParseConceptReference();
            if (type == null) return null;

            SkipWhitespace();
            if (!TryConsume("=")) return null;
            SkipWhitespace();

            var value = TryParseAttributeValue();
            if (value == null) return null;

            return new ConceptAttribute(type, value);
        }

        private ConceptReference? TryParseAttributeValue()
        {
            SkipWhitespace();
            if (_pos >= _input.Length) return null;

            // Concrete value: #number or #"string"
            if (_input[_pos] == '#')
            {
                var val = ParseConcreteValue();
                return new ConceptReference("concrete", val);
            }

            // Quoted string value (without #)
            if (_input[_pos] == '"')
            {
                var val = ParseQuotedStringValue();
                return new ConceptReference("concrete", val);
            }

            // Nested expression in parentheses
            if (_input[_pos] == '(')
            {
                _depth++;
                if (_depth > MaxDepth)
                    throw new InvalidOperationException($"Max nesting depth {MaxDepth} exceeded");

                _pos++; // skip '('
                SkipWhitespace();
                var nestedExpr = Parse(); // recursive
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == ')')
                    _pos++;
                _depth--;

                // Return the focus concept — the caller will replace with the pre-coordinated
                // concept ID from FHIR properties if available
                if (nestedExpr.FocusConcepts.Count > 0)
                    return nestedExpr.FocusConcepts[0];
                return new ConceptReference("nested", null);
            }

            // Simple concept reference
            return TryParseConceptReference();
        }

        private string ParseConcreteValue()
        {
            if (_pos < _input.Length && _input[_pos] == '#')
                _pos++; // skip '#'

            if (_pos < _input.Length && _input[_pos] == '"')
                return ParseQuotedStringValue();

            // Number (integer or decimal, possibly negative)
            int start = _pos;
            while (_pos < _input.Length)
            {
                var c = _input[_pos];
                if (char.IsDigit(c) || c == '.' || c == '-')
                    _pos++;
                else
                    break;
            }
            return _input[start.._pos].ToString();
        }

        private string ParseQuotedStringValue()
        {
            if (_pos < _input.Length && _input[_pos] == '"')
                _pos++; // skip opening quote

            int start = _pos;
            while (_pos < _input.Length && _input[_pos] != '"')
                _pos++;

            var value = _input[start.._pos].ToString();

            if (_pos < _input.Length && _input[_pos] == '"')
                _pos++; // skip closing quote

            return value;
        }

        private ConceptReference? TryParseConceptReference()
        {
            SkipWhitespace();
            if (_pos >= _input.Length || !char.IsDigit(_input[_pos])) return null;

            // Read concept ID (digits)
            int start = _pos;
            while (_pos < _input.Length && char.IsDigit(_input[_pos]))
                _pos++;

            var conceptId = _input[start.._pos].ToString();
            string? term = null;

            SkipWhitespace();

            // Optional pipe-delimited term
            if (_pos < _input.Length && _input[_pos] == '|')
            {
                _pos++; // skip opening pipe
                int termStart = _pos;
                while (_pos < _input.Length && _input[_pos] != '|')
                    _pos++;

                term = _input[termStart.._pos].ToString().Trim();

                if (_pos < _input.Length && _input[_pos] == '|')
                    _pos++; // skip closing pipe
            }

            return new ConceptReference(conceptId, term);
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
                _pos++;
        }

        private bool TryConsume(string s)
        {
            SkipWhitespace();
            if (_pos + s.Length > _input.Length) return false;

            for (int i = 0; i < s.Length; i++)
            {
                if (_input[_pos + i] != s[i]) return false;
            }

            _pos += s.Length;
            return true;
        }
    }

    // Internal parsed types (not exposed — mapped to existing visualization models)
    private record ParsedExpression(List<ConceptReference> FocusConcepts, ParsedRefinement? Refinement);
    private record ParsedRefinement(List<ConceptAttribute> UngroupedAttributes, List<AttributeGroup> Groups);
}

/// <summary>
/// Result of parsing a SNOMED CT normal form expression.
/// </summary>
public sealed class NormalFormResult
{
    public List<ConceptAttribute> UngroupedAttributes { get; } = new();
    public List<AttributeGroup> Groups { get; } = new();
}
