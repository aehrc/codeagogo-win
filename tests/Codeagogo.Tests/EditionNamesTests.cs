// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using FluentAssertions;

namespace Codeagogo.Tests;

/// <summary>
/// Tests for EditionNames module ID to edition name mappings.
/// </summary>
public class EditionNamesMapTests
{
    #region GetEditionName - Known Module IDs

    [Theory]
    [InlineData("900000000000207008", "International")]
    [InlineData("900000000000012004", "International")]  // Core module maps to International
    [InlineData("32506021000036107", "Australian")]
    [InlineData("929360061000036106", "Australian")]
    [InlineData("900062011000036108", "Australian Medicines Terminology")]
    [InlineData("731000124108", "US")]
    [InlineData("999000011000000103", "UK Clinical")]
    [InlineData("999000021000000109", "UK Drug")]
    [InlineData("83821000000107", "UK")]
    [InlineData("20621000087109", "Canadian")]
    [InlineData("21000210109", "New Zealand")]
    [InlineData("11000172109", "Belgian")]
    [InlineData("45991000052106", "Swedish")]
    [InlineData("11000146104", "Dutch")]
    [InlineData("449081005", "Spanish")]
    [InlineData("2011000195101", "Swiss")]
    [InlineData("554471000005108", "Danish")]
    [InlineData("51000202101", "Norwegian")]
    [InlineData("11000220105", "Irish")]
    [InlineData("11000221109", "Argentinian")]
    [InlineData("5631000179106", "Uruguayan")]
    [InlineData("11000181102", "Estonian")]
    [InlineData("17101000194103", "Singaporean")]
    public void GetEditionName_KnownModuleId_ReturnsCorrectName(string moduleId, string expectedName)
    {
        EditionNames.GetEditionName(moduleId).Should().Be(expectedName);
    }

    #endregion

    #region GetEditionName - Unknown Module IDs

    [Theory]
    [InlineData("999999999")]
    [InlineData("unknown-id")]
    [InlineData("0")]
    public void GetEditionName_UnknownModuleId_ReturnsUnknown(string moduleId)
    {
        EditionNames.GetEditionName(moduleId).Should().Be("Unknown");
    }

    #endregion

    #region GetEditionName - Null/Empty

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEditionName_NullOrEmpty_ReturnsUnknown(string? moduleId)
    {
        EditionNames.GetEditionName(moduleId).Should().Be("Unknown");
    }

    #endregion

    #region GetEditionNameOrModuleId - Known

    [Fact]
    public void GetEditionNameOrModuleId_KnownModuleId_ReturnsName()
    {
        EditionNames.GetEditionNameOrModuleId("900000000000207008")
            .Should().Be("International");
    }

    #endregion

    #region GetEditionNameOrModuleId - Unknown Returns Module ID

    [Fact]
    public void GetEditionNameOrModuleId_UnknownModuleId_ReturnsModuleId()
    {
        EditionNames.GetEditionNameOrModuleId("123456789012")
            .Should().Be("123456789012");
    }

    #endregion

    #region GetEditionNameOrModuleId - Null/Empty

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEditionNameOrModuleId_NullOrEmpty_ReturnsUnknown(string? moduleId)
    {
        EditionNames.GetEditionNameOrModuleId(moduleId).Should().Be("Unknown");
    }

    #endregion
}
