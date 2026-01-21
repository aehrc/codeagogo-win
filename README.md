# SNOMED Lookup (Windows)

![platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![UI](https://img.shields.io/badge/UI-WPF-green)

A lightweight Windows system tray utility for looking up **SNOMED CT concept IDs** from anywhere in the system.

Select a SNOMED CT concept ID in any application, press a global hotkey, and instantly see the concept's clinical terminology details in a popup near your cursor.

> **Note:** This is a developer/terminology power tool intended for internal use at CSIRO/AEHRC.

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

### Core Functionality
- **Global Hotkey** — Works system-wide in any application (default: `Ctrl+Shift+L`)
- **Instant Lookup** — Retrieves concept data from FHIR terminology server in real-time
- **Smart Selection** — Automatically reads selected text via simulated Ctrl+C
- **Multi-Edition Search** — Falls back through 15+ SNOMED CT editions if concept not found in International

### Display Information
- **Concept ID** — The SNOMED CT identifier
- **FSN** (Fully Specified Name) — The unambiguous clinical term with semantic tag
- **PT** (Preferred Term) — The commonly used clinical term
- **Status** — Whether the concept is active or inactive
- **Edition** — Which SNOMED CT edition contains the concept

### User Experience
- **System Tray App** — Runs quietly in the notification area, always accessible
- **Cursor-Anchored Popup** — Results appear near your mouse cursor
- **Copy Utilities** — One-click buttons to copy ID, FSN, PT, or combinations
- **Multiple Dismiss Options** — Close via X button, Escape key, or clicking outside

### Performance & Reliability
- **In-Memory Caching** — 6-hour TTL cache reduces API calls for repeated lookups
- **LRU Eviction** — Cache limited to 100 entries with least-recently-used eviction
- **Retry Logic** — Automatic retry with exponential backoff for transient failures
- **Thread-Safe** — Lock-based concurrency for safe parallel operations

### Configuration
- **Customizable Hotkey** — Choose your preferred key and modifiers
- **Configurable FHIR Endpoint** — Use alternative FHIR terminology servers
- **Debug Logging** — Optional detailed logging for troubleshooting
- **Diagnostic Export** — Export system info and logs for support

## Requirements

| Requirement | Details |
|-------------|---------|
| **Windows** | 10 or 11 |
| **Internet** | Required for FHIR terminology server queries |
| **.NET** | 8.0 Runtime (included in self-contained builds) |

## Installation

For detailed installation instructions, see **[INSTALL.md](INSTALL.md)**.

### Quick Start

1. Download the latest `SNOMED-Lookup-Windows.zip` from [Releases](../../releases)
2. Extract to a folder (e.g., `C:\Tools\SNOMED Lookup\`)
3. Run `SNOMED Lookup.exe`
4. Click "More info" → "Run anyway" if SmartScreen appears
5. The app appears in your system tray — you're ready to go!

## Usage

### Basic Lookup

1. **Select** a SNOMED CT concept ID in any application
   - Example: `73211009` (Diabetes mellitus)
   - The ID should be a 6-18 digit number

2. **Press** the global hotkey (default: `Ctrl+Shift+L`)

3. **View** the concept details in the popup that appears near your cursor

### Copy Options

The popup provides several copy buttons:

| Button | Copies |
|--------|--------|
| **Copy ID** | Just the concept ID |
| **Copy FSN** | The Fully Specified Name |
| **Copy PT** | The Preferred Term |
| **Copy ID & FSN** | Format: `ID \| FSN \|` |
| **Copy ID & PT** | Format: `ID \| PT \|` |

### Dismissing the Popup

| Action | Effect |
|--------|--------|
| Press `Escape` | Closes the popup |
| Click outside popup | Closes the popup |
| Click X button | Closes the popup |

### Tray Menu

Right-click the tray icon for options:
- **Settings** — Configure hotkey, FHIR endpoint, and logging
- **Exit** — Close the application

## Configuration

Access settings via the tray icon right-click menu → Settings.

### Hotkey Settings

- Click in the hotkey field and press your desired key combination
- Must include at least one modifier (Ctrl, Shift, Alt, or Win)
- Examples: `Ctrl+Shift+L`, `Ctrl+Alt+S`, `Win+K`

### FHIR Endpoint

- **Default**: `https://tx.ontoserver.csiro.au/fhir`
- Configure a custom FHIR R4 terminology server if needed
- Empty values fall back to the default

### Logging

- **Debug Logging**: Enable for detailed operation logs
- **Export Diagnostics**: Creates a text file with system info and recent logs

## Architecture

For a detailed technical overview, see **[ARCHITECTURE.md](ARCHITECTURE.md)**.

### High-Level Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    SNOMED Lookup Application                │
├─────────────────────────────────────────────────────────────┤
│  Presentation Layer                                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ System Tray │  │ Popup Window│  │ Settings Window     │  │
│  │ (WinForms)  │  │ (WPF)       │  │ (WPF)               │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  Service Layer                                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │ FhirClient  │  │ Selection   │  │ LruCache            │  │
│  │             │  │ Reader      │  │                     │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  External                                                   │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ FHIR Terminology Server (Ontoserver) via HTTPS         ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
```

### Key Components

| Component | Responsibility |
|-----------|----------------|
| `TrayAppContext` | System tray setup, hotkey registration, popup management |
| `FhirClient` | FHIR API communication, response parsing, caching |
| `ClipboardSelectionReader` | Captures selected text via simulated Ctrl+C |
| `LruCache` | Thread-safe LRU cache with TTL expiration |
| `PopupWindow` | Displays results with copy buttons |
| `SettingsWindow` | Application preferences UI |

### Project Structure

```
snomed-lookup-win/
├── SNOMEDLookup.sln           # Visual Studio solution
├── src/SNOMEDLookup/          # Main application
│   ├── App.xaml(.cs)          # Application entry point
│   ├── TrayAppContext.cs      # System tray and coordination
│   ├── FhirClient.cs          # FHIR terminology client
│   ├── LruCache.cs            # Thread-safe cache
│   ├── ClipboardSelectionReader.cs  # Selection capture
│   ├── PopupWindow.xaml(.cs)  # Results popup
│   ├── SettingsWindow.xaml(.cs)  # Settings dialog
│   ├── Settings.cs            # Persisted settings
│   ├── Models.cs              # Data models
│   └── Log.cs                 # Logging utilities
├── tests/SNOMEDLookup.Tests/  # Unit tests
│   ├── LruCacheTests.cs
│   ├── EditionNamesTests.cs
│   ├── ConceptResultTests.cs
│   └── LogTests.cs
├── README.md                  # This file
├── INSTALL.md                 # Installation guide
├── ARCHITECTURE.md            # Technical architecture
├── CONTRIBUTING.md            # Contribution guidelines
├── CHANGELOG.md               # Version history
└── PRIVACY.md                 # Privacy policy
```

## Development

### Prerequisites

- **.NET 8 SDK** or Visual Studio 2022 with ".NET desktop development" workload
- **Windows 10/11**
- Git

### Building

```powershell
# Clone the repository
git clone https://github.com/aehrc/snomed-lookup-win.git
cd snomed-lookup-win

# Restore and build
dotnet restore
dotnet build

# Or open in Visual Studio
start SNOMEDLookup.sln
```

### Running

```powershell
# Run from command line
dotnet run --project src/SNOMEDLookup

# Or press F5 in Visual Studio
```

### Creating a Release

```powershell
# Build self-contained single-file executable
dotnet publish src/SNOMEDLookup/SNOMEDLookup.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o out

# Output: out/SNOMED Lookup.exe
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
```

### Test Coverage

| Test Suite | Tests | Coverage |
|------------|-------|----------|
| `LruCacheTests` | 15 | Cache operations, TTL, LRU eviction, thread safety |
| `EditionNamesTests` | 38 | SNOMED edition module ID mapping |
| `ConceptResultTests` | 7 | Model properties and equality |
| `LogTests` | 9 | Logging utilities |
| **Total** | **69** | |

## Privacy

- **Selection Access**: Only reads selected text when the hotkey is pressed
- **Clipboard Restore**: Original clipboard contents are restored after reading
- **No Persistence**: No user data is stored to disk (except settings)
- **No Telemetry**: No analytics, tracking, or usage data collection
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

## Acknowledgements

- **CSIRO Ontoserver** — FHIR terminology services
- **SNOMED International** — SNOMED CT terminology standard
