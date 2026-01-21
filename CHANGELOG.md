# Changelog

All notable changes to SNOMED Lookup (Windows) are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Comprehensive unit test suite (69 tests)
- LRU cache with configurable size limit (100 entries)
- Retry logic with exponential backoff for transient network failures
- Architecture documentation (ARCHITECTURE.md)
- Contributing guidelines (CONTRIBUTING.md)
- Privacy policy (PRIVACY.md)
- This changelog

### Changed
- Enhanced README with comprehensive documentation matching macOS version
- Improved INSTALL.md with detailed troubleshooting guide

## [2.0.0] - 2025-01

### Added
- **FHIR-based terminology lookup** replacing Snowstorm API
- **Multi-edition search** across 15+ SNOMED CT editions
- **Selection reading** via simulated Ctrl+C (not just clipboard)
- **Copy buttons** for ID, FSN, PT, and combinations (ID & FSN, ID & PT)
- **FHIR endpoint configuration** in Settings
- **Debug logging toggle** in Settings
- **Diagnostic export** functionality
- **LRU cache** with 6-hour TTL and 100-entry limit
- **Click-to-dismiss** popup (click outside, Escape key, X button)

### Changed
- Redesigned popup window with loading states and error display
- Expanded settings window with FHIR and logging sections
- Improved error messages and user feedback

### Technical
- New `FhirClient.cs` for FHIR API communication
- New `LruCache.cs` for thread-safe caching
- Enhanced `ClipboardSelectionReader.cs` with `keybd_event` for selection capture
- Updated `PopupWindow` with copy button handlers and dismiss logic
- Updated `SettingsWindow` with FHIR endpoint and logging options

## [1.0.0] - 2024-09

### Added
- Initial release
- Global hotkey activation (Ctrl+Shift+L)
- SNOMED CT concept lookup via Snowstorm API
- Popup display near cursor with concept details:
  - Concept ID
  - Fully Specified Name (FSN)
  - Preferred Term (PT)
  - Active/inactive status
- Configurable hotkey (key and modifiers)
- Basic clipboard-based concept ID reading
- System tray integration
- Settings window for hotkey configuration
- File-based logging

### Technical
- WPF-based user interface
- WinForms NotifyIcon for system tray
- Win32 RegisterHotKey for global hotkey
- JSON settings persistence
- Windows 10+ support
- .NET 8 runtime
