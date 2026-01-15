// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using Codeagogo;

namespace Codeagogo.Tests;

public class EditionNamesTests
{
    [Theory]
    [InlineData("900000000000207008", "International")]
    [InlineData("900000000000012004", "International")]
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
    public void GetEditionName_ReturnsCorrectName_ForKnownModuleIds(string moduleId, string expectedName)
    {
        var result = EditionNames.GetEditionName(moduleId);
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData("unknown-module-id")]
    [InlineData("123456789")]
    [InlineData("not-a-real-module")]
    public void GetEditionName_ReturnsUnknown_ForUnknownModuleIds(string moduleId)
    {
        var result = EditionNames.GetEditionName(moduleId);
        Assert.Equal("Unknown", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEditionName_ReturnsUnknown_ForNullOrEmptyInput(string? moduleId)
    {
        var result = EditionNames.GetEditionName(moduleId);
        Assert.Equal("Unknown", result);
    }

    [Theory]
    [InlineData("900000000000207008", "International")]
    [InlineData("32506021000036107", "Australian")]
    public void GetEditionNameOrModuleId_ReturnsName_ForKnownModuleIds(string moduleId, string expectedName)
    {
        var result = EditionNames.GetEditionNameOrModuleId(moduleId);
        Assert.Equal(expectedName, result);
    }

    [Theory]
    [InlineData("unknown-module-id")]
    [InlineData("123456789")]
    public void GetEditionNameOrModuleId_ReturnsModuleId_ForUnknownModuleIds(string moduleId)
    {
        var result = EditionNames.GetEditionNameOrModuleId(moduleId);
        Assert.Equal(moduleId, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetEditionNameOrModuleId_ReturnsUnknown_ForNullOrEmptyInput(string? moduleId)
    {
        var result = EditionNames.GetEditionNameOrModuleId(moduleId);
        Assert.Equal("Unknown", result);
    }
}
