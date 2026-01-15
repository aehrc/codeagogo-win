// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Builds URLs for opening concepts in the Shrimp terminology browser.
/// </summary>
/// <remarks>
/// Shrimp is a terminology browser that can display concept details
/// from various code systems. This utility constructs the appropriate
/// URL format based on the code system and available metadata.
/// </remarks>
public static class ShrimpUrlBuilder
{
    /// <summary>
    /// The base URL for the Shrimp browser.
    /// </summary>
    private const string ShrimpBaseUrl = "https://ontoserver.csiro.au/shrimp/";

    /// <summary>
    /// SNOMED CT Core module ID (not a proper edition).
    /// </summary>
    private const string CoreModuleId = "900000000000012004";

    /// <summary>
    /// SNOMED CT International edition module ID.
    /// </summary>
    private const string InternationalModuleId = "900000000000207008";

    /// <summary>
    /// Builds a Shrimp URL for a given concept.
    /// </summary>
    /// <param name="conceptId">The concept code/ID</param>
    /// <param name="system">The code system URI (e.g., "http://snomed.info/sct")</param>
    /// <param name="moduleId">The SNOMED CT module/edition ID (optional, for SNOMED only)</param>
    /// <param name="version">The version string (optional, for non-SNOMED systems)</param>
    /// <param name="fhirEndpoint">The FHIR server endpoint URL</param>
    /// <returns>A URL for opening the concept in Shrimp, or null if URL construction fails</returns>
    public static string? BuildUrl(
        string conceptId,
        string? system,
        string? moduleId = null,
        string? version = null,
        string? fhirEndpoint = null)
    {
        if (string.IsNullOrEmpty(system))
        {
            Log.Info("Cannot build Shrimp URL: no system specified");
            return null;
        }

        var queryParams = new List<string>();

        // Add concept parameter (always present)
        queryParams.Add($"concept={Uri.EscapeDataString(conceptId)}");

        // Determine if this is SNOMED CT
        bool isSNOMED = system.StartsWith("http://snomed.info/sct") ||
                        system.StartsWith("http://snomed.info/xsct");

        if (isSNOMED)
        {
            // SNOMED CT: use version URI and edition-specific ValueSet
            if (!string.IsNullOrEmpty(moduleId))
            {
                // Map SNOMED CT Core module to International edition for Shrimp
                var mappedModuleId = moduleId == CoreModuleId ? InternationalModuleId : moduleId;

                var versionUri = $"http://snomed.info/sct/{mappedModuleId}";
                queryParams.Add($"version={Uri.EscapeDataString(versionUri)}");

                // ValueSet: http://snomed.info/sct/[moduleId]?fhir_vs
                var valuesetUri = $"http://snomed.info/sct/{mappedModuleId}?fhir_vs";
                queryParams.Add($"valueset={Uri.EscapeDataString(valuesetUri)}");
            }
            else
            {
                // Fallback: use International edition if no module/version info
                Log.Info("Building Shrimp URL for SNOMED CT without module/version info");
                var valuesetUri = $"http://snomed.info/sct/{InternationalModuleId}?fhir_vs";
                queryParams.Add($"valueset={Uri.EscapeDataString(valuesetUri)}");
            }
        }
        else
        {
            // Non-SNOMED systems: include system parameter
            queryParams.Add($"system={Uri.EscapeDataString(system)}");

            // Add version if available
            if (!string.IsNullOrEmpty(version))
            {
                queryParams.Add($"version={Uri.EscapeDataString(version)}");
            }

            // ValueSet format depends on system
            var valuesetUri = BuildValueSetUri(system);
            queryParams.Add($"valueset={Uri.EscapeDataString(valuesetUri)}");
        }

        // Add FHIR endpoint (always present) - normalize by removing trailing slash
        if (!string.IsNullOrEmpty(fhirEndpoint))
        {
            var normalizedEndpoint = fhirEndpoint.TrimEnd('/');
            queryParams.Add($"fhir={Uri.EscapeDataString(normalizedEndpoint)}");
        }

        var url = ShrimpBaseUrl + "?" + string.Join("&", queryParams);
        Log.Info($"Built Shrimp URL: {url}");
        return url;
    }

    /// <summary>
    /// Builds a Shrimp URL from a ConceptResult.
    /// </summary>
    /// <param name="result">The concept lookup result</param>
    /// <param name="fhirEndpoint">The FHIR server endpoint URL</param>
    /// <returns>A URL for opening the concept in Shrimp, or null if URL construction fails</returns>
    public static string? BuildUrl(ConceptResult result, string fhirEndpoint)
    {
        // For SNOMED CT results, System is typically null - default to snomed.info/sct
        var system = result.System ?? "http://snomed.info/sct";

        return BuildUrl(
            conceptId: result.ConceptId,
            system: system,
            moduleId: result.ModuleId,
            version: ExtractVersion(result.Branch),
            fhirEndpoint: fhirEndpoint
        );
    }

    /// <summary>
    /// Extracts version string from a branch/edition string.
    /// </summary>
    /// <remarks>
    /// For non-SNOMED systems, the branch often contains the version in parentheses,
    /// e.g., "LOINC (2.81)". This method extracts that version string.
    /// </remarks>
    /// <param name="branch">The branch/edition string</param>
    /// <returns>The version string, or null if not found</returns>
    private static string? ExtractVersion(string? branch)
    {
        if (string.IsNullOrEmpty(branch)) return null;

        // Try to extract version from parentheses: "System (2.81)" -> "2.81"
        var start = branch.IndexOf('(');
        var end = branch.IndexOf(')');

        if (start >= 0 && end > start)
        {
            return branch.Substring(start + 1, end - start - 1).Trim();
        }

        return null;
    }

    /// <summary>
    /// Builds the ValueSet URI for a given code system.
    /// </summary>
    /// <param name="system">The code system URI</param>
    /// <returns>The implicit ValueSet URI for that system</returns>
    private static string BuildValueSetUri(string system)
    {
        return system switch
        {
            "http://loinc.org" => "http://loinc.org/vs",
            var s when s.StartsWith("http://hl7.org/fhir/sid/icd-10") => $"{s}?fhir_vs",
            var s when s.StartsWith("http://hl7.org/fhir/sid/icd-9") => $"{s}?fhir_vs",
            "http://www.nlm.nih.gov/research/umls/rxnorm" => "http://www.nlm.nih.gov/research/umls/rxnorm?fhir_vs",
            _ => $"{system}?fhir_vs"
        };
    }
}
