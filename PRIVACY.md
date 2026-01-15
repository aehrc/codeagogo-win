# Privacy Policy -- Codeagogo (Windows)

_Last updated: 2026-03_

Codeagogo is a Windows utility intended for internal use by clinicians, terminologists, and developers. The application is designed to minimise data access and avoid persistent storage.

---

## What information the app accesses

### Selected text (on demand only)

When the user presses any of the 5 configured global hotkeys (Lookup, Search, Replace, ECL Format, or Browser), the app:
- Issues a standard **Copy** action (Ctrl+C) to the foreground application
- Reads the resulting clipboard text to extract concept IDs, search terms, or ECL expressions
- Immediately restores the clipboard to its previous contents

The app does **not** monitor selections continuously and does **not** read the clipboard unless explicitly triggered by the user.

### Network access

When a concept lookup, search, or replace operation is triggered, the app makes outbound HTTPS requests to retrieve concept details from:

- **CSIRO Ontoserver** (default)
  - https://tx.ontoserver.csiro.au/fhir
  - FHIR R4 terminology server

The following data may be transmitted as part of these requests:
- **Concept IDs** -- sent during lookup and replace operations via `CodeSystem/$lookup`
- **Search terms** -- sent during search operations via `ValueSet/$expand`
- **Code system identifiers** -- sent to specify which code system to query (SNOMED CT, LOINC, ICD-10, RxNorm, etc.)
- **Edition/version identifiers** -- sent to filter SNOMED CT editions

- **User-Agent header** -- includes the app version and an anonymous install ID (random UUID) for counting unique installations from server logs. Format: `Codeagogo/<version> (Windows; <install-id>)`

No personal data is included in any request.

### Automatic update checks

On startup and every 24 hours, the app checks for updates by making HTTPS requests to:

- **GitHub Releases** -- `https://github.com/aehrc/codeagogo-win/releases`

These requests check for newer versions and download delta update packages if available. No personal data, concept IDs, or usage information is sent — only standard HTTP headers (User-Agent with app version).

---

## Anonymous Install ID

On first launch, Codeagogo generates a random UUID (e.g., `a1b2c3d4-e5f6-...`) that serves as an anonymous install identifier. This ID:

- Is included in the `User-Agent` header on all FHIR server requests
- Contains no personal data and cannot identify you
- Is used solely to count unique installations from server logs
- Is stored locally in `settings.json`
- Can be reset at any time via **Settings > Privacy**, which generates a new random UUID

---

## What information is NOT collected

Codeagogo does **not**:

- Collect or transmit personal information
- Store clipboard contents (beyond temporary processing)
- Persist user data to disk (except application settings and code system configuration)
- Collect usage analytics or telemetry (only an anonymous install ID is included in requests -- no personal data, no behavior tracking)
- Track user behaviour
- Access files, folders, or other system resources
- Use background monitoring or keylogging
- Record what text you select or copy

---

## Permissions

### Keyboard input

The app uses Win32 `keybd_event` and `SendInput` to:
- Simulate Ctrl+C to copy the current selection
- Simulate Shift+Arrow keys to re-select text after paste (Replace feature)

These are only invoked when the user explicitly presses a global hotkey.

### Clipboard access

The app accesses the Windows clipboard to:
- Snapshot current contents before simulating Ctrl+C
- Read the copied text to extract concept IDs, search terms, or ECL expressions
- Restore the original clipboard contents afterward
- Temporarily place replacement text for paste operations (Replace and ECL Format features)

Clipboard access occurs only during an active operation triggered by the user.

### Network access

The app requires outbound network access to:
- Resolve DNS
- Make HTTPS requests to the FHIR terminology server

No inbound network connections are used.

### WebView2 (Visualization)

The app uses WebView2 to render SVG concept diagrams. WebView2 operates locally and does not make external network requests for visualization rendering.

---

## Data retention

- **Settings**: Stored locally in `%LOCALAPPDATA%\AEHRC\Codeagogo\settings.json`
- **Code Systems**: Stored locally in `%LOCALAPPDATA%\AEHRC\Codeagogo\` as part of settings
- **Logs**: Stored locally in `%LOCALAPPDATA%\AEHRC\Codeagogo\logs\app.log`
- **Cache**: In-memory only, discarded when the app exits
- **Clipboard**: Original contents restored immediately after reading

No concept data, search history, or user activity is persisted.

---

## Third-party services

The app relies on FHIR terminology services, by default CSIRO Ontoserver.

- **CSIRO Ontoserver**: https://ontoserver.csiro.au/
- Users may configure alternative FHIR R4 terminology servers
- The Shrimp Browser hotkey opens a URL in the user's default web browser

Use of these services is subject to their respective terms and policies.

---

## Local data

The following data is stored locally on your computer:

| Data | Location | Purpose |
|------|----------|---------|
| Settings | `%LOCALAPPDATA%\AEHRC\Codeagogo\settings.json` | User preferences (hotkeys, code systems, endpoint, logging, anonymous install ID) |
| Logs | `%LOCALAPPDATA%\AEHRC\Codeagogo\logs\app.log` | Troubleshooting and diagnostics |

To completely remove all local data:
1. Exit the application
2. Delete the folder: `%LOCALAPPDATA%\AEHRC\Codeagogo\`

---

## Changes

This privacy policy may be updated if the app's functionality changes. Any material changes will be documented in the repository.

---

## Contact

For questions or concerns about this app or its privacy characteristics, contact the project maintainers via the GitHub repository.
