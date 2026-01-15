// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using Codeagogo.Visualization;
using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for visualization model types used in SNOMED CT concept diagrams.
/// </summary>
public class VisualizationModelsTests
{
    #region ConceptVisualizationData

    [Fact]
    public void ConceptVisualizationData_CanBeCreatedWithRequiredProperties()
    {
        var data = new ConceptVisualizationData
        {
            ConceptId = "73211009",
            PreferredTerm = "Diabetes mellitus",
            FullySpecifiedName = "Diabetes mellitus (disorder)",
            SufficientlyDefined = false
        };

        data.ConceptId.Should().Be("73211009");
        data.PreferredTerm.Should().Be("Diabetes mellitus");
        data.FullySpecifiedName.Should().Be("Diabetes mellitus (disorder)");
        data.SufficientlyDefined.Should().BeFalse();
    }

    [Fact]
    public void ConceptVisualizationData_DefaultParents_IsEmptyList()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };

        data.Parents.Should().NotBeNull();
        data.Parents.Should().BeEmpty();
    }

    [Fact]
    public void ConceptVisualizationData_DefaultAttributeGroups_IsEmptyList()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };

        data.AttributeGroups.Should().NotBeNull();
        data.AttributeGroups.Should().BeEmpty();
    }

    [Fact]
    public void ConceptVisualizationData_DefaultUngroupedAttributes_IsEmptyList()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };

        data.UngroupedAttributes.Should().NotBeNull();
        data.UngroupedAttributes.Should().BeEmpty();
    }

    [Fact]
    public void ConceptVisualizationData_OptionalPropertiesAreNullByDefault()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };

        data.PreferredTerm.Should().BeNull();
        data.FullySpecifiedName.Should().BeNull();
    }

    [Fact]
    public void ConceptVisualizationData_SufficientlyDefined_DefaultIsFalse()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };

        data.SufficientlyDefined.Should().BeFalse();
    }

    [Fact]
    public void ConceptVisualizationData_CanAddParents()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };
        data.Parents.Add(new ConceptReference("404684003", "Clinical finding"));
        data.Parents.Add(new ConceptReference("64572001", "Disease"));

        data.Parents.Should().HaveCount(2);
        data.Parents[0].ConceptId.Should().Be("404684003");
        data.Parents[1].ConceptId.Should().Be("64572001");
    }

    [Fact]
    public void ConceptVisualizationData_CanAddUngroupedAttributes()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };
        data.UngroupedAttributes.Add(new ConceptAttribute(
            new ConceptReference("116676008", "Morphology"),
            new ConceptReference("85828009", "Disease")));

        data.UngroupedAttributes.Should().HaveCount(1);
    }

    [Fact]
    public void ConceptVisualizationData_CanAddAttributeGroups()
    {
        var data = new ConceptVisualizationData { ConceptId = "73211009" };
        var group = new AttributeGroup
        {
            GroupNumber = 1,
            Attributes =
            {
                new ConceptAttribute(
                    new ConceptReference("116676008", "Morphology"),
                    new ConceptReference("85828009", "Disease"))
            }
        };
        data.AttributeGroups.Add(group);

        data.AttributeGroups.Should().HaveCount(1);
        data.AttributeGroups[0].GroupNumber.Should().Be(1);
    }

    #endregion

    #region ConceptReference

    [Fact]
    public void ConceptReference_SameValues_AreEqual()
    {
        var a = new ConceptReference("73211009", "Diabetes mellitus");
        var b = new ConceptReference("73211009", "Diabetes mellitus");

        a.Should().Be(b);
    }

    [Fact]
    public void ConceptReference_DifferentIds_AreNotEqual()
    {
        var a = new ConceptReference("73211009", "Diabetes mellitus");
        var b = new ConceptReference("404684003", "Diabetes mellitus");

        a.Should().NotBe(b);
    }

    [Fact]
    public void ConceptReference_DifferentTerms_AreNotEqual()
    {
        var a = new ConceptReference("73211009", "Diabetes mellitus");
        var b = new ConceptReference("73211009", "Type 2 diabetes");

        a.Should().NotBe(b);
    }

    [Fact]
    public void ConceptReference_NullTerm_IsValid()
    {
        var reference = new ConceptReference("73211009", null);

        reference.ConceptId.Should().Be("73211009");
        reference.Term.Should().BeNull();
    }

    [Fact]
    public void ConceptReference_WithNullTerm_EqualityWorks()
    {
        var a = new ConceptReference("73211009", null);
        var b = new ConceptReference("73211009", null);

        a.Should().Be(b);
    }

    [Fact]
    public void ConceptReference_NullVsNonNullTerm_NotEqual()
    {
        var a = new ConceptReference("73211009", null);
        var b = new ConceptReference("73211009", "Diabetes");

        a.Should().NotBe(b);
    }

    #endregion

    #region ConceptAttribute

    [Fact]
    public void ConceptAttribute_SameValues_AreEqual()
    {
        var type = new ConceptReference("116676008", "Morphology");
        var value = new ConceptReference("85828009", "Disease");

        var a = new ConceptAttribute(type, value);
        var b = new ConceptAttribute(type, value);

        a.Should().Be(b);
    }

    [Fact]
    public void ConceptAttribute_DifferentType_NotEqual()
    {
        var value = new ConceptReference("85828009", "Disease");
        var a = new ConceptAttribute(new ConceptReference("116676008", "Morphology"), value);
        var b = new ConceptAttribute(new ConceptReference("363698007", "Finding site"), value);

        a.Should().NotBe(b);
    }

    [Fact]
    public void ConceptAttribute_DifferentValue_NotEqual()
    {
        var type = new ConceptReference("116676008", "Morphology");
        var a = new ConceptAttribute(type, new ConceptReference("85828009", "Disease"));
        var b = new ConceptAttribute(type, new ConceptReference("113331007", "Endocrine"));

        a.Should().NotBe(b);
    }

    [Fact]
    public void ConceptAttribute_HasTypeAndValue()
    {
        var type = new ConceptReference("116676008", "Morphology");
        var value = new ConceptReference("85828009", "Disease");
        var attr = new ConceptAttribute(type, value);

        attr.Type.Should().Be(type);
        attr.Value.Should().Be(value);
    }

    #endregion

    #region AttributeGroup

    [Fact]
    public void AttributeGroup_HasCorrectGroupNumber()
    {
        var group = new AttributeGroup { GroupNumber = 3 };

        group.GroupNumber.Should().Be(3);
    }

    [Fact]
    public void AttributeGroup_DefaultAttributes_IsEmptyList()
    {
        var group = new AttributeGroup();

        group.Attributes.Should().NotBeNull();
        group.Attributes.Should().BeEmpty();
    }

    [Fact]
    public void AttributeGroup_CanAddAttributes()
    {
        var group = new AttributeGroup { GroupNumber = 1 };
        group.Attributes.Add(new ConceptAttribute(
            new ConceptReference("116676008", "Morphology"),
            new ConceptReference("85828009", "Disease")));
        group.Attributes.Add(new ConceptAttribute(
            new ConceptReference("363698007", "Finding site"),
            new ConceptReference("113331007", "Endocrine")));

        group.Attributes.Should().HaveCount(2);
    }

    [Fact]
    public void AttributeGroup_DefaultGroupNumber_IsZero()
    {
        var group = new AttributeGroup();

        group.GroupNumber.Should().Be(0);
    }

    #endregion

    #region NormalFormResult

    [Fact]
    public void NormalFormResult_DefaultUngroupedAttributes_IsEmptyList()
    {
        var result = new NormalFormResult();

        result.UngroupedAttributes.Should().NotBeNull();
        result.UngroupedAttributes.Should().BeEmpty();
    }

    [Fact]
    public void NormalFormResult_DefaultGroups_IsEmptyList()
    {
        var result = new NormalFormResult();

        result.Groups.Should().NotBeNull();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public void NormalFormResult_CanAddUngroupedAttributes()
    {
        var result = new NormalFormResult();
        result.UngroupedAttributes.Add(new ConceptAttribute(
            new ConceptReference("116676008", "Morphology"),
            new ConceptReference("85828009", "Disease")));

        result.UngroupedAttributes.Should().HaveCount(1);
    }

    [Fact]
    public void NormalFormResult_CanAddGroups()
    {
        var result = new NormalFormResult();
        result.Groups.Add(new AttributeGroup { GroupNumber = 1 });
        result.Groups.Add(new AttributeGroup { GroupNumber = 2 });

        result.Groups.Should().HaveCount(2);
    }

    #endregion
}
