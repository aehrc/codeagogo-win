// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Maps SNOMED CT module IDs to human-readable edition names.
/// </summary>
public static class EditionNames
{
    private static readonly Dictionary<string, string> ModuleToEdition = new()
    {
        // International
        ["900000000000207008"] = "International",
        ["900000000000012004"] = "International", // Core module

        // Australian
        ["32506021000036107"] = "Australian",
        ["929360061000036106"] = "Australian",
        ["900062011000036108"] = "Australian Medicines Terminology",

        // US
        ["731000124108"] = "US",

        // UK
        ["999000011000000103"] = "UK Clinical",
        ["999000021000000109"] = "UK Drug",
        ["83821000000107"] = "UK",

        // Canadian
        ["20621000087109"] = "Canadian",

        // New Zealand
        ["21000210109"] = "New Zealand",

        // Belgian
        ["11000172109"] = "Belgian",

        // Swedish
        ["45991000052106"] = "Swedish",

        // Dutch
        ["11000146104"] = "Dutch",

        // Spanish
        ["449081005"] = "Spanish",

        // Swiss
        ["2011000195101"] = "Swiss",

        // Danish
        ["554471000005108"] = "Danish",

        // Norwegian
        ["51000202101"] = "Norwegian",

        // Irish
        ["11000220105"] = "Irish",

        // Argentinian
        ["11000221109"] = "Argentinian",

        // Uruguayan
        ["5631000179106"] = "Uruguayan",

        // Estonian
        ["11000181102"] = "Estonian",

        // Singaporean
        ["17101000194103"] = "Singaporean",
    };

    /// <summary>
    /// Gets the human-readable edition name for a module ID.
    /// </summary>
    /// <param name="moduleId">The SNOMED CT module ID</param>
    /// <returns>The edition name, or "Unknown" if not recognized</returns>
    public static string GetEditionName(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return "Unknown";

        return ModuleToEdition.TryGetValue(moduleId, out var name) ? name : "Unknown";
    }

    /// <summary>
    /// Gets the human-readable edition name for a module ID, or returns the module ID itself if unknown.
    /// </summary>
    /// <param name="moduleId">The SNOMED CT module ID</param>
    /// <returns>The edition name, or the module ID if not recognized, or "Unknown" if null/empty</returns>
    public static string GetEditionNameOrModuleId(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return "Unknown";

        return ModuleToEdition.TryGetValue(moduleId, out var name) ? name : moduleId;
    }
}
