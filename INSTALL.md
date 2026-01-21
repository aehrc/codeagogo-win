# Install (Windows)

## System Requirements

- Windows 10 or Windows 11
- Internet connection (for FHIR terminology lookups)

## Quick Start

1. Download the latest `SNOMED.Lookup.zip` from [Releases](https://github.com/aehrc/snomed-lookup-win/releases) or GitHub Actions artifacts.
2. Extract to a folder (e.g., `C:\Tools\SNOMED Lookup\`).
3. Run `SNOMED Lookup.exe`.
4. The app appears as a tray icon in the system notification area.

## First Run Notes

- **SmartScreen warning**: Windows may show a warning for unsigned binaries. Click "More info" → "Run anyway".
- **Firewall prompt**: Allow network access for FHIR terminology lookups.

## Usage

### Basic Lookup

1. Select a SNOMED CT concept ID (e.g., `73211009`) in any application.
2. Press **Ctrl+Shift+L** (default hotkey).
3. A popup appears near your cursor showing:
   - Concept ID
   - Fully Specified Name (FSN)
   - Preferred Term (PT)
   - Status (active/inactive)
   - Edition (International, Australian, US, etc.)

### Copy Buttons

The popup includes five copy buttons:
- **Copy ID**: Copies just the concept ID
- **Copy FSN**: Copies the Fully Specified Name
- **Copy PT**: Copies the Preferred Term
- **Copy ID & FSN**: Copies formatted as `{id} | {fsn} |`
- **Copy ID & PT**: Copies formatted as `{id} | {pt} |`

### Dismissing the Popup

- Press **Escape**
- Click outside the popup
- Click the **X** button in the top-right corner

### Tray Menu

Right-click the tray icon for options:
- **Settings**: Configure hotkey, FHIR endpoint, and logging
- **Exit**: Close the application

## Settings

Access via tray icon → Settings:

### Hotkey Configuration

- Click in the hotkey field
- Press your desired key combination (e.g., Ctrl+Alt+S)
- Must include at least one modifier (Ctrl, Shift, Alt, or Win)

### FHIR Endpoint

- Default: `https://tx.ontoserver.csiro.au/fhir`
- Can be changed to any FHIR R4 terminology server supporting `$lookup`

### Debug Logging

- Enable for verbose logging during troubleshooting
- Logs include API requests, responses, and timing information

### Export Diagnostics

- Creates a text file with system info and recent logs
- Useful for reporting issues

## File Locations

| File | Location |
|------|----------|
| Settings | `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\settings.json` |
| Logs | `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\logs\app.log` |

## Troubleshooting

### "No SNOMED CT ID found"

- Ensure you've selected text containing only digits
- Try copying the ID to clipboard manually, then press the hotkey

### Popup doesn't appear

- Check the tray icon is visible (may be in overflow area)
- Verify the hotkey isn't used by another application
- Check logs for errors

### Lookup fails or times out

- Verify internet connectivity
- Check if the FHIR endpoint is accessible in a browser
- Try the default Ontoserver endpoint

### Hotkey not working

- Some applications may capture hotkeys before Windows
- Try a different key combination in Settings

## Uninstall

1. Right-click tray icon → Exit
2. Delete the application folder
3. Optionally delete settings: `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\`
