using System.Collections.Generic;

namespace SNOMEDLookup;

public sealed record ConceptResult(
    string ConceptId,
    string Branch,
    string? Fsn,
    string? Pt,
    bool? Active,
    string? EffectiveTime,
    string? ModuleId,
    string? Edition = null
)
{
    public string ActiveText => Active switch
    {
        true => "active",
        false => "inactive",
        _ => "-"
    };
}

/// <summary>
/// Provides mapping from SNOMED CT module IDs to human-readable edition names.
/// </summary>
public static class EditionNames
{
    private static readonly Dictionary<string, string> ModuleToEdition = new()
    {
        // International
        ["900000000000207008"] = "International",
        ["900000000000012004"] = "International",

        // Australian
        ["32506021000036107"] = "Australian",
        ["929360061000036106"] = "Australian",
        ["900062011000036108"] = "Australian Medicines Terminology",

        // United States
        ["731000124108"] = "US",

        // United Kingdom
        ["999000011000000103"] = "UK Clinical",
        ["999000021000000109"] = "UK Drug",
        ["83821000000107"] = "UK",

        // Canada
        ["20621000087109"] = "Canadian",

        // New Zealand
        ["21000210109"] = "New Zealand",

        // Belgium
        ["11000172109"] = "Belgian",

        // Sweden
        ["45991000052106"] = "Swedish",

        // Netherlands
        ["11000146104"] = "Dutch",

        // Spain
        ["449081005"] = "Spanish",

        // Switzerland
        ["2011000195101"] = "Swiss",

        // Denmark
        ["554471000005108"] = "Danish",

        // Norway
        ["51000202101"] = "Norwegian",

        // Ireland
        ["11000220105"] = "Irish",

        // Argentina
        ["11000221109"] = "Argentinian",

        // Uruguay
        ["5631000179106"] = "Uruguayan",

        // Estonia
        ["11000181102"] = "Estonian",

        // Singapore
        ["17101000194103"] = "Singaporean"
    };

    /// <summary>
    /// Gets the friendly edition name for a given module ID.
    /// </summary>
    public static string GetEditionName(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return "Unknown";

        return ModuleToEdition.TryGetValue(moduleId, out var name) ? name : "Unknown";
    }

    /// <summary>
    /// Gets the friendly edition name, with module ID fallback for unknown modules.
    /// </summary>
    public static string GetEditionNameOrModuleId(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
            return "Unknown";

        return ModuleToEdition.TryGetValue(moduleId, out var name) ? name : moduleId;
    }
}
