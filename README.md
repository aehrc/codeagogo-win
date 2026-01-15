# <img src="docs/icon.png" width="32" height="32" alt="Codeagogo logo" style="vertical-align: middle;"> Codeagogo (Windows)

[![build](https://github.com/aehrc/codeagogo-win/actions/workflows/build.yml/badge.svg)](https://github.com/aehrc/codeagogo-win/actions/workflows/build.yml)
[![license](https://img.shields.io/badge/license-Apache%202.0-blue)](LICENSE)
![platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

A powerful Windows system tray utility for looking up, searching, and working with **clinical terminology codes** from anywhere in the system. Supports SNOMED CT, LOINC, ICD-10, RxNorm, and other code systems via configurable FHIR R4 terminology servers.

Select a concept ID in any application, press a global hotkey, and instantly see the concept's clinical terminology details in a popup near your cursor. Search for concepts by term, batch-annotate IDs with their display names, format ECL expressions, and visualize SNOMED CT concept hierarchies.

> **Looking for the macOS version?** See [Codeagogo for macOS](https://github.com/aehrc/codeagogo).

Developed by CSIRO's Australian e-Health Research Centre (AEHRC).

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [Development](#development)
- [Testing](#testing)
- [Privacy](#privacy)
- [Contributing](#contributing)

## Features

### Global Hotkeys

Codeagogo provides six system-wide hotkeys that work in any application:

| Hotkey | Function | Description |
|--------|----------|-------------|
| `Ctrl+Shift+L` | **Concept Lookup** | Look up a selected concept ID and display its details |
| `Ctrl+Shift+S` | **Concept Search** | Search for concepts by term across configured code systems |
| `Ctrl+Shift+R` | **Bulk Replace** | Annotate selected concept IDs with their display terms |
| `Ctrl+Shift+E` | **ECL Format/Minify** | Toggle formatting of Expression Constraint Language expressions |
| `Ctrl+Shift+V` | **ECL Workbench** | Open the ECL editor with live evaluation |
| `Ctrl+Shift+B` | **Shrimp Browser** | Open the selected concept in the SNOMED CT browser |

All hotkeys are fully customizable via the Settings window using built-in hotkey recorder controls.

### Concept Lookup (`Ctrl+Shift+L`)
- **Instant Lookup** -- Retrieves concept data from FHIR terminology server in real-time
- **Smart Selection** -- Automatically reads selected text via simulated Ctrl+C with clipboard restore
- **Multi-Edition Search** -- Falls back through 15+ SNOMED CT editions if concept not found in International
- **Multi-Code System** -- Supports SNOMED CT, LOINC, ICD-10, RxNorm, and custom code systems
- **SCTID Validation** -- Verhoeff check digit algorithm validates SNOMED CT identifiers
- **Copy Utilities** -- One-click buttons to copy ID, FSN, PT, or combinations
- **Inactive Warning** -- Visual indicator when a concept is inactive
- **Concept Visualization** -- SVG diagram rendering of SNOMED CT normal forms via WebView2 with PNG export

### Concept Search (`Ctrl+Shift+S`)
- **Term Search** -- Find concepts by typing search terms
- **Debounced Input** -- Searches trigger after a typing pause to reduce server load
- **Multiple Code Systems** -- Search across any configured code system
- **SNOMED Edition Filtering** -- Filter SNOMED CT searches by edition
- **ValueSet/$expand** -- Uses FHIR ValueSet expansion for search results
- **Result Actions** -- Copy concept details or insert them into the source application

### Bulk Replace (`Ctrl+Shift+R`)
- **Batch Annotation** -- Look up multiple concept IDs in selected text via ValueSet/$expand
- **Smart Toggle** -- Adds terms if missing, removes them if already present
- **Inactive Prefix** -- Marks inactive concepts with a visual prefix
- **FSN/PT Selection** -- Choose between Fully Specified Name or Preferred Term format
- **Text Re-selection** -- After paste, re-selects the inserted text (up to 1000 chars)

### ECL Format/Minify (`Ctrl+Shift+E`)
- **ecl-core Powered** -- Uses the `@aehrc/ecl-core` TypeScript library (shared with macOS) via Jint
- **Format Toggle** -- Switches between formatted (indented) and minified ECL with 9 configurable options
- **Structured Errors** -- Parse errors with line and column information
- **Concept Validation** -- Background check for inactive/unknown concepts after formatting

### ECL Workbench (`Ctrl+Shift+V`)
- **Monaco Editor** -- Full ECL editor with syntax highlighting, bracket matching, and minimap
- **FHIR Autocomplete** -- Concept suggestions powered by the configured terminology server
- **Inline Diagnostics** -- Real-time parse error highlighting with red squiggles
- **Live Evaluation** -- Results update on 500ms debounce or explicit Ctrl+Enter
- **Results Panel** -- Matching concepts with semantic tags, Shrimp links, and diagram buttons
- **Empty Launch** -- Open with no selection to draft ECL from scratch
- **Panel Persistence** -- Stays visible across app switches; hides on Escape (preserving content)

### ECL Reference
- **Knowledge Base** -- ~50 articles covering ECL operators, refinements, filters, patterns, grammar, and history
- **Searchable** -- Filter articles by keyword across name, summary, and content
- **Expandable** -- Click articles to expand Markdown content with code blocks and tables

### Shrimp Browser (`Ctrl+Shift+B`)
- **Browser Launch** -- Opens the selected SNOMED CT concept in the Shrimp browser
- **URL Builder** -- Constructs correct Shrimp URLs for concept navigation

### User Experience
- **System Tray App** -- Runs quietly in the notification area, always accessible
- **Cursor-Anchored Popup** -- Results appear near your mouse cursor
- **Progress HUD** -- Transparent overlay for long-running operations
- **Clipboard Restore** -- Original clipboard contents are preserved and restored
- **Multiple Dismiss Options** -- Close via X button, Escape key, or clicking outside

### Performance & Reliability
- **In-Memory Caching** -- 6-hour TTL cache reduces API calls for repeated lookups
- **LRU Eviction** -- Cache limited to 100 entries with least-recently-used eviction
- **Retry Logic** -- Automatic retry with exponential backoff for transient failures
- **Thread-Safe** -- Lock-based concurrency for safe parallel operations
- **Automatic Updates** -- Checks for updates on startup and every 24 hours via Velopack; delta packages download only what's changed
- **Code Signed** -- Official releases are signed with CSIRO's DigiCert certificate, eliminating Windows SmartScreen warnings

### Configuration
- **6 Customizable Hotkeys** -- Each with a dedicated hotkey recorder control
- **Configurable Code Systems** -- Add, remove, and reorder code systems with custom URLs
- **Configurable FHIR Endpoint** -- Use alternative FHIR terminology servers
- **Start with Windows** -- Optional auto-start on login (configurable in Settings and welcome screen)
- **Debug Logging** -- Optional detailed logging for troubleshooting
- **Diagnostic Export** -- Export system info and logs for support

## Requirements

| Requirement | Details |
|-------------|---------|
| **Windows** | 10 or 11 |
| **Internet** | Required for FHIR terminology server queries |
| **.NET** | 8.0 Runtime (included in self-contained builds) |
| **WebView2** | Required for concept visualization (included in Windows 11, available for Windows 10) |

## Installation

For detailed installation instructions, see **[INSTALL.md](INSTALL.md)**.

### Quick Start

1. Download the latest `Codeagogo-Windows.zip` from [Releases](../../releases)
2. Extract to a folder (e.g., `C:\Tools\Codeagogo\`)
3. Run `Codeagogo.exe`
4. Click "More info" then "Run anyway" if SmartScreen appears
5. The app appears in your system tray -- you're ready to go!

## Usage

### Concept Lookup

1. **Select** a concept ID in any application
   - Example: `73211009` (SNOMED CT - Diabetes mellitus)
   - SNOMED CT IDs are validated using the Verhoeff check digit algorithm

2. **Press** `Ctrl+Shift+L` (default hotkey)

3. **View** the concept details in the popup that appears near your cursor

### Concept Search

1. **Press** `Ctrl+Shift+S` to open the search window
2. **Type** a search term (e.g., "diabetes")
3. **Select** the code system and edition to search
4. **Click** a result to copy or insert it

### Bulk Replace

1. **Select** text containing concept IDs in any application
2. **Press** `Ctrl+Shift+R`
3. The selected text is annotated with display terms (or terms are removed if already present)

### ECL Format/Minify

1. **Select** an ECL expression in any application
2. **Press** `Ctrl+Shift+E`
3. The expression toggles between formatted and minified forms

### ECL Workbench

1. **Select** an ECL expression (optional — press with no selection for blank editor)
2. **Press** `Ctrl+Shift+V`
3. Edit ECL with syntax highlighting, autocomplete, and live evaluation results

### ECL Reference

- Right-click the tray icon, then **ECL Reference...**
- Browse ~50 knowledge articles about ECL operators, refinements, and patterns

### Shrimp Browser

1. **Select** a SNOMED CT concept ID in any application
2. **Press** `Ctrl+Shift+B`
3. The concept opens in the Shrimp SNOMED CT browser

### Copy Options

The lookup popup provides several copy buttons:

| Button | Copies |
|--------|--------|
| **Copy ID** | Just the concept ID |
| **Copy FSN** | The Fully Specified Name |
| **Copy PT** | The Preferred Term |
| **Copy ID & FSN** | Format: `ID \| FSN \|` |
| **Copy ID & PT** | Format: `ID \| PT \|` |

### Dismissing Popups

| Action | Effect |
|--------|--------|
| Press `Escape` | Closes the popup |
| Click outside popup | Closes the popup |
| Click X button | Closes the popup |

### Tray Menu

Right-click the tray icon for options:
- **Settings** -- Configure hotkeys, code systems, FHIR endpoint, and logging
- **Exit** -- Close the application

## Configuration

Access settings via the tray icon right-click menu, then Settings.

### Hotkey Settings

- Each of the 6 hotkeys has a dedicated **hotkey recorder** control
- Click the recorder field and press your desired key combination
- Must include at least one modifier (Ctrl, Shift, Alt, or Win)
- All hotkeys are independently configurable

### Code Systems

- Configure multiple code systems (SNOMED CT, LOINC, ICD-10, RxNorm, etc.)
- Each code system has a name, system URL, and optional version
- Add, remove, and reorder code systems as needed

### FHIR Endpoint

- **Default**: `https://tx.ontoserver.csiro.au/fhir`
- Configure a custom FHIR R4 terminology server if needed
- Empty values fall back to the default

### Logging

- **Debug Logging**: Enable for detailed operation logs
- **Export Diagnostics**: Creates a text file with system info and recent logs

## Architecture

For technical details (data flow, concurrency model, caching, design decisions), see **[ARCHITECTURE.md](ARCHITECTURE.md)**.

### Project Structure

```
src/Codeagogo/              # Main WPF application (ECL via ecl-core/Jint bridge)
src/Codeagogo/Visualization/ # SNOMED CT concept diagram SVG rendering (WebView2 or browser)
src/Codeagogo/Controls/     # Reusable WPF controls (HotKeyRecorder)
tests/Codeagogo.Tests/      # Unit tests
tests/Codeagogo.E2ETests/   # E2E tests (FlaUI-based, requires desktop)
```

## Development

### Prerequisites

- **.NET 8 SDK** or Visual Studio 2022 with ".NET desktop development" workload
- **Windows 10/11**
- Git

### Building

```powershell
# Clone the repository
git clone https://github.com/aehrc/Codeagogo-win.git
cd Codeagogo-win

# Restore and build
dotnet restore
dotnet build

# Or open in Visual Studio
start Codeagogo.sln
```

### Running

```powershell
# Run from command line
dotnet run --project src/Codeagogo

# Or press F5 in Visual Studio
```

### Code Style

The project uses an `.editorconfig` file with `EnforceCodeStyleInBuild` enabled. Code style rules are enforced at build time, ensuring consistent formatting across all contributions.

### Creating a Release

```powershell
# Build self-contained single-file executable
dotnet publish src/Codeagogo/Codeagogo.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o out

# Output: out/Codeagogo.exe
```

## Testing

### Running Tests

```powershell
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~LruCacheTests"

# Run only the main test project
dotnet test tests/Codeagogo.Tests
```

### Test Projects

| Test Project | Description |
|--------------|-------------|
| `Codeagogo.Tests` | Unit tests for all features |
| `Codeagogo.E2ETests` | FlaUI-based end-to-end UI tests |

### Key Test Suites

| Test Suite | Coverage |
|------------|----------|
| `SCTIDValidatorTests` | Verhoeff check digit validation |
| `ECLLexerTests` | ECL tokenization |
| `ECLParserTests` | ECL parsing and AST construction |
| `ECLFormatterTests` | ECL formatting and minification |
| `SearchViewModelTests` | Search logic and debouncing |
| `OntoserverClientTests` | FHIR client operations |
| `ConceptCacheTests` | Cache behavior and eviction |
| `CodeSystemSettingsTests` | Code system configuration |
| `LruCacheTests` | Cache operations, TTL, LRU eviction, thread safety |
| `EditionNamesTests` | SNOMED edition module ID mapping |
| `ConceptResultTests` | Model properties and equality |
| `LogTests` | Logging utilities |

## Privacy

- **Selection Access**: Only reads selected text when a hotkey is pressed
- **Clipboard Restore**: Original clipboard contents are restored after reading
- **No Persistence**: No user data is stored to disk (except settings)
- **Anonymous Install ID**: A random UUID is included in requests for install counting -- contains no personal data, resettable via Settings > Privacy
- **Welcome Screen**: One-time welcome window shown on first launch
- **Network**: Only HTTPS requests to the configured FHIR server

For complete details, see **[PRIVACY.md](PRIVACY.md)**.

## Contributing

We welcome contributions! Please see **[CONTRIBUTING.md](CONTRIBUTING.md)** for guidelines.

### Quick Contribution Guide

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes with appropriate tests
4. Ensure all tests pass (`dotnet test`)
5. Submit a pull request

## License

This project is licensed under the [Apache License 2.0](LICENSE).

## Acknowledgements

- **CSIRO Ontoserver** -- FHIR terminology services
- **SNOMED International** -- SNOMED CT terminology standard
