using System;
using System.IO;
using System.Text.Json;

namespace SNOMEDLookup;

public sealed class Settings
{
    // Default: Ctrl+Shift+L
    public uint HotKeyModifiers { get; set; } = 0x0002 | 0x0004; // MOD_CONTROL | MOD_SHIFT
    public uint HotKeyVirtualKey { get; set; } = 0x4C; // 'L'

    // FHIR endpoint configuration
    public string FhirBaseUrl { get; set; } = "https://tx.ontoserver.csiro.au/fhir";

    // Debug logging toggle
    public bool DebugLoggingEnabled { get; set; } = false;

    public static Settings Load()
    {
        try
        {
            var path = PathToSettings();
            if (!File.Exists(path)) return new Settings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }
        catch
        {
            return new Settings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathToSettings())!);
        File.WriteAllText(PathToSettings(), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string PathToSettings()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AEHRC",
            "SNOMED Lookup"
        );
        return Path.Combine(dir, "settings.json");
    }
}
