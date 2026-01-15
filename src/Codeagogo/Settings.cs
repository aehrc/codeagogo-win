// Copyright 2026 CSIRO. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.Text.Json;

namespace Codeagogo;

public enum TermFormat
{
    FSN,
    PT
}

public enum InsertFormat
{
    IdOnly,
    PtOnly,
    FsnOnly,
    IdPipePT,
    IdPipeFSN
}

public sealed class Settings
{
    // Hotkey: Ctrl+Shift+L (Lookup)
    public uint LookupHotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint LookupHotKeyVirtualKey { get; set; } = 0x4C; // 'L'

    // Hotkey: Ctrl+Shift+S (Search)
    public uint SearchHotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint SearchHotKeyVirtualKey { get; set; } = 0x53; // 'S'

    // Hotkey: Ctrl+Shift+R (Replace)
    public uint ReplaceHotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint ReplaceHotKeyVirtualKey { get; set; } = 0x52; // 'R'

    // Hotkey: Ctrl+Shift+E (ECL Format)
    public uint EclFormatHotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint EclFormatHotKeyVirtualKey { get; set; } = 0x45; // 'E'

    // Hotkey: Ctrl+Shift+B (Shrimp Browser)
    public uint ShrimpHotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint ShrimpHotKeyVirtualKey { get; set; } = 0x42; // 'B'

    // Hotkey: Ctrl+Shift+V (Evaluate ECL)
    public uint EvaluateHotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint EvaluateHotKeyVirtualKey { get; set; } = 0x56; // 'V'

    // Evaluate ECL settings
    public int EvaluateResultLimit { get; set; } = 50;

    // ECL Workbench window geometry
    public double WorkbenchWidth { get; set; } = 750;
    public double WorkbenchHeight { get; set; } = 650;
    public double WorkbenchLeft { get; set; } = -1;
    public double WorkbenchTop { get; set; } = -1;
    public double WorkbenchSplitRatio { get; set; } = 0.6; // editor fraction

    // Replace feature settings
    public TermFormat ReplaceTermFormat { get; set; } = TermFormat.FSN;
    public bool PrefixInactive { get; set; } = true;

    // Insert format settings
    public InsertFormat DefaultInsertFormat { get; set; } = InsertFormat.IdPipeFSN;

    // API settings
    public string FhirBaseUrl { get; set; } = "https://tx.ontoserver.csiro.au/fhir/";

    // Startup
    public bool StartWithWindows { get; set; } = true;

    // Debug settings
    public bool DebugLogging { get; set; } = false;

    // Privacy settings

    /// <summary>
    /// Anonymous install identifier used for counting active installations.
    /// Auto-generated on first launch; no personal data is collected.
    /// </summary>
    public string? InstallId { get; set; }

    // First-launch welcome
    public bool WelcomeShown { get; set; } = false;

    // Backward compatibility: migrate old HotKeyModifiers/HotKeyVirtualKey to LookupHotKey*
    public uint HotKeyModifiers
    {
        get => LookupHotKeyModifiers;
        set => LookupHotKeyModifiers = value;
    }

    public uint HotKeyVirtualKey
    {
        get => LookupHotKeyVirtualKey;
        set => LookupHotKeyVirtualKey = value;
    }

    public static Settings Load()
    {
        Settings settings;
        try
        {
            var path = PathToSettings();
            if (!File.Exists(path))
            {
                settings = new Settings();
            }
            else
            {
                var json = File.ReadAllText(path);
                settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch
        {
            settings = new Settings();
        }

        // Auto-generate InstallId on first launch or when migrating from older settings
        if (string.IsNullOrEmpty(settings.InstallId))
        {
            settings.InstallId = Guid.NewGuid().ToString();
            settings.Save();
        }

        return settings;
    }

    /// <summary>
    /// Resets the anonymous install identifier to a new random value.
    /// </summary>
    public void ResetInstallId()
    {
        InstallId = Guid.NewGuid().ToString();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathToSettings())!);
        File.WriteAllText(PathToSettings(), JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
        }));
    }

    private static string PathToSettings()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AEHRC",
            "Codeagogo"
        );
        return Path.Combine(dir, "settings.json");
    }
}
