// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text.Json;

namespace Codeagogo;

/// <summary>
/// Manages configured code systems for multi-terminology support.
/// </summary>
public sealed class CodeSystemSettings
{
    public List<ConfiguredCodeSystem> Systems { get; set; } = new()
    {
        new ConfiguredCodeSystem("http://snomed.info/sct", "SNOMED CT", true),
        new ConfiguredCodeSystem("http://loinc.org", "LOINC", false),
        new ConfiguredCodeSystem("http://hl7.org/fhir/sid/icd-10", "ICD-10", false),
        new ConfiguredCodeSystem("http://www.nlm.nih.gov/research/umls/rxnorm", "RxNorm", false)
    };

    /// <summary>
    /// Gets the URIs of all enabled code systems.
    /// </summary>
    public IEnumerable<string> EnabledSystemUris => Systems
        .Where(s => s.Enabled)
        .Select(s => s.Uri);

    /// <summary>
    /// Saves the code system configuration to disk.
    /// </summary>
    public void Save()
    {
        var path = PathToCodeSystems();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Loads the code system configuration from disk.
    /// </summary>
    /// <returns>The loaded settings, or a new instance with defaults if loading fails.</returns>
    public static CodeSystemSettings Load()
    {
        try
        {
            var path = PathToCodeSystems();
            if (!File.Exists(path))
                return new CodeSystemSettings();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CodeSystemSettings>(json) ?? new CodeSystemSettings();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load code system settings: {ex.Message}");
            return new CodeSystemSettings();
        }
    }

    /// <summary>
    /// Gets the file path for code system configuration.
    /// </summary>
    public static string PathToCodeSystems()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AEHRC",
            "Codeagogo"
        );
        return Path.Combine(dir, "codesystems.json");
    }
}
