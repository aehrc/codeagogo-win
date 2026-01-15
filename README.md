# SNOMED Lookup (Windows)

A small Windows 11 tray utility that looks up SNOMED CT concept IDs from your current selection and displays the term in a popup near the cursor.

- Repo: https://github.com/aehrc/snomed-lookup-win
- Git:  https://github.com/aehrc/snomed-lookup-win.git
- Hotkey (initial): Ctrl+Shift+L
- Terminology source (initial): SNOMED Snowstorm Concept Lookup Service (searches across editions)

## Install

See [INSTALL.md](INSTALL.md).

## Build

### Build on Windows
Install **.NET 8 SDK** (or Visual Studio 2022 with the ".NET desktop development" workload), then run:

```powershell
dotnet restore
dotnet build -c Release
dotnet publish src\SNOMEDLookup\SNOMEDLookup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out
```

### Build from macOS
You cannot build Windows desktop apps natively on macOS, but you can:
- use GitHub Actions (Windows runner) to build artefacts, or
- build in a Windows 11 VM (Parallels/UTM).

## Troubleshooting
Logs: `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\logs\app.log`
