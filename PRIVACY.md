# Privacy Policy – SNOMED Lookup (Windows)

_Last updated: 2025-01_

SNOMED Lookup is a lightweight Windows utility intended for internal use by clinicians, terminologists, and developers. The application is designed to minimise data access and avoid persistent storage.

---

## What information the app accesses

### Selected text (on demand only)

When the user presses the configured global hotkey, the app:
- Issues a standard **Copy** action (Ctrl+C) to the foreground application
- Reads the resulting clipboard text to extract a SNOMED CT concept ID
- Immediately restores the clipboard to its previous contents

The app does **not** monitor selections continuously and does **not** read the clipboard unless explicitly triggered by the user.

### Network access

When a valid SNOMED CT concept ID is detected, the app makes outbound HTTPS requests to retrieve concept details from:

- **CSIRO Ontoserver** (default)
  - https://tx.ontoserver.csiro.au/fhir
  - FHIR R4 terminology server

Only the concept ID is transmitted as part of these requests.

---

## What information is NOT collected

SNOMED Lookup does **not**:

- Collect or transmit personal information
- Store clipboard contents (beyond temporary processing)
- Persist user data to disk (except application settings)
- Collect usage analytics or telemetry
- Track user behaviour
- Access files, folders, or other system resources
- Use background monitoring or keylogging
- Record what text you select or copy

---

## Permissions

### Keyboard input

The app uses Win32 `keybd_event` to:
- Simulate Ctrl+C to copy the current selection

This is only invoked when the user explicitly presses the global hotkey.

### Clipboard access

The app accesses the Windows clipboard to:
- Snapshot current contents before simulating Ctrl+C
- Read the copied text to extract concept IDs
- Restore the original clipboard contents afterward

Clipboard access occurs only during an active lookup triggered by the user.

### Network access

The app requires outbound network access to:
- Resolve DNS
- Make HTTPS requests to the FHIR terminology server

No inbound network connections are used.

---

## Data retention

- **Settings**: Stored locally in `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\settings.json`
- **Logs**: Stored locally in `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\logs\app.log`
- **Cache**: In-memory only, discarded when the app exits
- **Clipboard**: Original contents restored immediately after reading

No concept data, search history, or user activity is persisted.

---

## Third-party services

The app relies on FHIR terminology services, by default CSIRO Ontoserver.

- **CSIRO Ontoserver**: https://ontoserver.csiro.au/
- Users may configure alternative FHIR R4 terminology servers

Use of these services is subject to their respective terms and policies.

---

## Local data

The following data is stored locally on your computer:

| Data | Location | Purpose |
|------|----------|---------|
| Settings | `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\settings.json` | User preferences (hotkey, endpoint, logging) |
| Logs | `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\logs\app.log` | Troubleshooting and diagnostics |

To completely remove all local data:
1. Exit the application
2. Delete the folder: `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\`

---

## Changes

This privacy policy may be updated if the app's functionality changes. Any material changes will be documented in the repository.

---

## Contact

For questions or concerns about this app or its privacy characteristics, contact the project maintainers via the GitHub repository.
