// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

namespace Codeagogo;

/// <summary>
/// Result of looking up a concept from the terminology server.
/// </summary>
/// <param name="ConceptId">The concept code (SNOMED CT ID or other code system identifier)</param>
/// <param name="Branch">Human-readable edition/version name</param>
/// <param name="Fsn">The Fully Specified Name (unambiguous term with semantic tag)</param>
/// <param name="Pt">The Preferred Term / Display name</param>
/// <param name="Active">Whether the concept is currently active</param>
/// <param name="EffectiveTime">The effective date when this concept version was published (YYYYMMDD)</param>
/// <param name="ModuleId">The module that contains this concept (SNOMED CT specific)</param>
/// <param name="System">The code system URI (null for SNOMED CT)</param>
/// <param name="Edition">The edition identifier for this concept</param>
public sealed record ConceptResult(
    string ConceptId,
    string Branch,
    string? Fsn,
    string? Pt,
    bool? Active,
    string? EffectiveTime,
    string? ModuleId,
    string? System = null,
    string? Edition = null
)
{
    /// <summary>
    /// Human-readable representation of the active status.
    /// </summary>
    public string ActiveText => Active switch
    {
        true => "active",
        false => "inactive",
        _ => "-"
    };

    /// <summary>
    /// Human-readable name for the code system.
    /// </summary>
    public string SystemName => System switch
    {
        null or "http://snomed.info/sct" or "http://snomed.info/xsct" => "SNOMED CT",
        "http://loinc.org" => "LOINC",
        "http://www.nlm.nih.gov/research/umls/rxnorm" => "RxNorm",
        "http://hl7.org/fhir/sid/icd-10-cm" => "ICD-10-CM",
        "http://hl7.org/fhir/sid/icd-10" => "ICD-10",
        "http://hl7.org/fhir/sid/icd-9-cm" => "ICD-9-CM",
        _ => System.Split('/').LastOrDefault() ?? System
    };

    /// <summary>
    /// Whether this result is from a SNOMED CT code system.
    /// </summary>
    public bool IsSnomedCT => System == null || System.Contains("snomed.info");
}
