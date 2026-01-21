# SNOMED Lookup (Windows)

A Windows system tray utility that looks up SNOMED CT concept IDs from your current selection and displays detailed information in a popup near the cursor.

## Features

- **Multi-edition search**: Searches across 15+ SNOMED CT editions (International, Australian, US, UK, Canadian, and more)
- **Selection reading**: Automatically reads selected text via simulated Ctrl+C
- **Clipboard fallback**: Falls back to clipboard if no selection is available
- **Copy buttons**: Quick copy for Concept ID, FSN, PT, or combinations
- **Configurable hotkey**: Default Ctrl+Shift+L, customizable in settings
- **Configurable FHIR endpoint**: Default Ontoserver, can point to any FHIR terminology server
- **Debug logging**: Optional verbose logging for troubleshooting
- **Diagnostic export**: Export system info and recent logs for support

## Links

- Repository: https://github.com/aehrc/snomed-lookup-win
- Terminology server: [Ontoserver](https://ontoserver.csiro.au/) (FHIR R4)

## Install

See [INSTALL.md](INSTALL.md).

## Build

### Prerequisites

- .NET 8 SDK (or Visual Studio 2022 with ".NET desktop development" workload)

### Build commands

```powershell
# Restore and build
dotnet restore
dotnet build -c Release

# Run tests
dotnet test

# Publish self-contained executable
dotnet publish src\SNOMEDLookup\SNOMEDLookup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

### Build from macOS

Windows desktop apps cannot be built natively on macOS. Options:
- Use GitHub Actions (Windows runner) to build artifacts
- Build in a Windows 11 VM (Parallels/UTM)

## Architecture

```
src/SNOMEDLookup/
├── App.xaml.cs                    # Application entry point
├── TrayAppContext.cs              # System tray icon and menu
├── FhirClient.cs                  # FHIR terminology server client
├── LruCache.cs                    # Thread-safe LRU cache with TTL
├── ClipboardSelectionReader.cs    # Selection reading via SendInput
├── PopupWindow.xaml(.cs)          # Results popup with copy buttons
├── SettingsWindow.xaml(.cs)       # Settings dialog
├── Settings.cs                    # Persisted user settings
├── Models.cs                      # ConceptResult and EditionNames
└── Log.cs                         # Logging utilities

tests/SNOMEDLookup.Tests/
├── LruCacheTests.cs               # Cache functionality tests
├── EditionNamesTests.cs           # Edition mapping tests
├── ConceptResultTests.cs          # Model tests
└── LogTests.cs                    # Logging utility tests
```

## Configuration

Settings are stored in: `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\settings.json`

| Setting | Default | Description |
|---------|---------|-------------|
| HotKeyModifiers | 0x0006 (Ctrl+Shift) | Modifier keys for hotkey |
| HotKeyVirtualKey | 0x4C (L) | Virtual key code for hotkey |
| FhirBaseUrl | https://tx.ontoserver.csiro.au/fhir | FHIR terminology server |
| DebugLoggingEnabled | false | Enable verbose debug logging |

## Troubleshooting

**Logs location**: `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\logs\app.log`

**Export diagnostics**: Settings > Export Diagnostics to create a support file

**Common issues**:
- "No SNOMED CT ID found": Ensure you've selected a valid concept ID (digits only)
- Popup doesn't appear: Check the hotkey isn't conflicting with another application
- Lookup fails: Verify network connectivity and FHIR endpoint in settings

## License

Apache 2.0
