# Changelog

All notable changes to Codeagogo (Windows) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [1.0.2] - 2026-04-01

## [1.0.1] - 2026-03-31

## [1.0.0] - 2026-03-31

## [1.0.0] - 2026-03-31

### Features
- **Concept Lookup** (`Ctrl+Shift+L`) -- Look up SNOMED CT, LOINC, ICD-10, and RxNorm concepts from any application
- **Concept Search** (`Ctrl+Shift+S`) -- Term-based search via FHIR `ValueSet/$expand` with debounced input and edition filtering
- **Bulk Replace** (`Ctrl+Shift+R`) -- Batch annotation of concept IDs with display terms, smart toggle (add/remove), FSN/PT format selection
- **ECL Format/Minify** (`Ctrl+Shift+E`) -- Toggle ECL expressions between formatted and minified using ecl-core
- **ECL Workbench** (`Ctrl+Shift+V`) -- Monaco-based ECL editor with syntax highlighting, FHIR autocomplete, inline diagnostics, and live evaluation
- **Shrimp Browser** (`Ctrl+Shift+B`) -- Open selected SNOMED CT concept in the Shrimp browser
- **ECL Reference Panel** -- 50 searchable ECL knowledge articles grouped by category
- **Concept Visualization** -- SVG diagram rendering of SNOMED CT normal forms with edition-correct display terms, green concrete values, and pre-coordinated concept resolution
- **Multi-Code System Support** -- Configurable code systems (SNOMED CT, LOINC, ICD-10, RxNorm, etc.)
- **Multi-Edition SNOMED CT** -- Automatic fallback across 15+ editions for namespaced SCTIDs
- **SCTID Validation** -- Verhoeff check digit algorithm for SNOMED CT identifiers
- **Automatic Updates** -- Velopack integration checks GitHub Releases on startup and every 24 hours with delta packages
- **Code Signing** -- DigiCert KeyLocker signing via CI, eliminating Windows SmartScreen warnings
- **Start with Windows** -- Optional auto-start on login, configurable in Settings and welcome screen
- **FlaUI UIA3 Selection Capture** -- COM-based UI Automation as primary selection strategy with Ctrl+C fallback for browsers
- **System Tray App** -- Runs in the notification area with context menu for all features
- **Clipboard Restore** -- Preserves and restores original clipboard contents during operations
- **Welcome Screen** -- First-launch welcome with mailing list, GitHub star, and startup options
- **Anonymous Install Metrics** -- Random UUID per install in User-Agent header for usage counting
