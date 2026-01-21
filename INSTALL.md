# Installing SNOMED Lookup on Windows

SNOMED Lookup is a small Windows utility that lets you look up SNOMED CT concept IDs from anywhere in Windows using a global hotkey.

Because this is an internal tool and is not code-signed, Windows SmartScreen will show a warning the first time you open it. This is expected.

---

## Step-by-step installation

### 1. Download

Download the latest `SNOMED-Lookup-Windows.zip` from the project's **Releases** page or GitHub Actions artifacts.

---

### 2. Extract and install

1. Right-click the downloaded zip file and select **Extract All...**
2. Choose a destination folder (e.g., `C:\Tools\SNOMED Lookup\`)
3. Click **Extract**

---

### 3. Run the app (first run)

When you first try to open the app, Windows SmartScreen may display a warning.

#### Handling SmartScreen

1. Click **More info**
2. Click **Run anyway**

You only need to do this once.

---

### 4. Verify installation

After launching, you should see:
- A small icon in the system tray (notification area)
- Right-click the icon to access Settings and Exit

---

## Using SNOMED Lookup

### Basic lookup

1. Select a SNOMED CT concept ID (e.g., `73211009`) in any application
2. Press **Ctrl+Shift+L** (default hotkey)
3. A popup appears near your cursor showing concept details

### Copy buttons

The popup includes five copy buttons:

| Button | What it copies |
|--------|----------------|
| Copy ID | Just the concept ID |
| Copy FSN | The Fully Specified Name |
| Copy PT | The Preferred Term |
| Copy ID & FSN | Format: `ID \| FSN \|` |
| Copy ID & PT | Format: `ID \| PT \|` |

### Dismissing the popup

- Press **Escape**
- Click outside the popup
- Click the **X** button

### Changing the hotkey

1. Right-click the tray icon → **Settings**
2. Click in the hotkey field
3. Press your desired key combination
4. Click **Save**

---

## Configuration

### Settings location

Settings are stored in:
```
%LOCALAPPDATA%\AEHRC\SNOMED Lookup\settings.json
```

### Available settings

| Setting | Default | Description |
|---------|---------|-------------|
| Hotkey | Ctrl+Shift+L | Global keyboard shortcut |
| FHIR Endpoint | https://tx.ontoserver.csiro.au/fhir | Terminology server URL |
| Debug Logging | Off | Enable verbose logging |

### FHIR endpoint

The default endpoint is CSIRO's Ontoserver. You can configure a different FHIR R4 terminology server that supports the `CodeSystem/$lookup` operation.

### Debug logging

Enable debug logging when troubleshooting issues. Logs include:
- API request/response details
- Selection reading operations
- Cache hits/misses
- Timing information

### Export diagnostics

Click **Export Diagnostics** in Settings to create a text file containing:
- System information (OS, .NET version)
- Application settings
- Recent log entries

---

## File locations

| File | Location |
|------|----------|
| Application | Where you extracted it |
| Settings | `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\settings.json` |
| Logs | `%LOCALAPPDATA%\AEHRC\SNOMED Lookup\logs\app.log` |

---

## Troubleshooting

### Nothing happens when I press the hotkey

1. **Check the app is running** — Look for the icon in the system tray
2. **Check tray overflow** — The icon may be hidden in the overflow area (click the ^ arrow)
3. **Verify the hotkey** — Open Settings to see the configured hotkey
4. **Check for conflicts** — Another application may be using the same hotkey

### "No SNOMED CT ID found" message

- Ensure you have selected text containing only digits
- The app expects 6-18 digit SNOMED CT concept IDs
- Try copying the ID to clipboard manually before pressing the hotkey

### Popup doesn't appear at cursor

- The popup appears near the cursor position when the hotkey was pressed
- Some applications may move focus, affecting cursor position

### Network errors or timeouts

1. **Check internet connectivity**
2. **Verify FHIR endpoint** — Try the default Ontoserver URL in Settings
3. **Check firewall** — Ensure the app can make outbound HTTPS connections
4. **Corporate proxy** — Some networks may block or require proxy configuration

### App doesn't start

1. **Check .NET 8 Runtime** — Self-contained builds include the runtime, but framework-dependent builds require it
2. **Run as Administrator** — Try right-click → Run as administrator
3. **Check antivirus** — Some antivirus software may block unsigned executables

### Hotkey stops working

- The hotkey may have been captured by another application
- Restart SNOMED Lookup to re-register the hotkey
- Try a different hotkey combination

---

## Running at startup

To start SNOMED Lookup automatically when Windows starts:

1. Press `Win+R`, type `shell:startup`, press Enter
2. Create a shortcut to `SNOMED Lookup.exe` in this folder

---

## Uninstalling

To remove SNOMED Lookup:

1. Right-click the tray icon → **Exit**
2. Delete the application folder
3. (Optional) Delete settings and logs:
   ```
   %LOCALAPPDATA%\AEHRC\SNOMED Lookup\
   ```
4. (Optional) Remove startup shortcut if created

---

## Support

For issues, questions, or feedback, please contact the project maintainers or raise an issue in the GitHub repository.
