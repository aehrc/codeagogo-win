// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo.Visualization;

/// <summary>
/// Represents the full visualization data for a SNOMED CT concept.
/// </summary>
public sealed class ConceptVisualizationData
{
    /// <summary>The focus concept being visualized.</summary>
    public required string ConceptId { get; init; }

    /// <summary>Preferred term of the focus concept.</summary>
    public string? PreferredTerm { get; init; }

    /// <summary>Fully specified name of the focus concept.</summary>
    public string? FullySpecifiedName { get; init; }

    /// <summary>Whether the concept is fully defined (true) or primitive (false).</summary>
    public bool SufficientlyDefined { get; init; }

    /// <summary>IS-A parent concepts.</summary>
    public List<ConceptReference> Parents { get; init; } = new();

    /// <summary>Defining relationships (grouped and ungrouped).</summary>
    public List<AttributeGroup> AttributeGroups { get; init; } = new();

    /// <summary>Ungrouped attributes (role group 0).</summary>
    public List<ConceptAttribute> UngroupedAttributes { get; set; } = new();
}

/// <summary>
/// A reference to a SNOMED CT concept (ID + display term).
/// </summary>
public sealed record ConceptReference(string ConceptId, string? Term);

/// <summary>
/// A group of attributes (SNOMED CT role group).
/// </summary>
public sealed class AttributeGroup
{
    public int GroupNumber { get; init; }
    public List<ConceptAttribute> Attributes { get; set; } = new();
}

/// <summary>
/// A single defining relationship attribute (type → value).
/// </summary>
public sealed record ConceptAttribute(
    ConceptReference Type,
    ConceptReference Value
);
