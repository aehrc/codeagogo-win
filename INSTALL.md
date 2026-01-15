# Installing Codeagogo on Windows

Codeagogo is a Windows utility that lets you look up, search, replace, format, and visualize clinical terminology codes from anywhere in Windows using global hotkeys. It supports SNOMED CT, LOINC, ICD-10, RxNorm, and other configurable code systems.

Official releases are code-signed by CSIRO and should not trigger Windows SmartScreen warnings. If you build from source, unsigned executables may show a SmartScreen warning on first run.

---

## Step-by-step installation

### 1. Download

Download the latest `Codeagogo-Windows.zip` from the project's **Releases** page or GitHub Actions artifacts.

---

### 2. Extract and install

1. Right-click the downloaded zip file and select **Extract All...**
2. Choose a destination folder (e.g., `C:\Tools\Codeagogo\`)
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

## Using Codeagogo

### Global Hotkeys

Codeagogo provides 5 global hotkeys that work in any application:

| Hotkey | Function | Description |
|--------|----------|-------------|
| `Ctrl+Shift+L` | **Concept Lookup** | Look up a selected concept ID and display its details |
| `Ctrl+Shift+S` | **Concept Search** | Search for concepts by term across configured code systems |
| `Ctrl+Shift+R` | **Bulk Replace** | Annotate selected concept IDs with their display terms |
| `Ctrl+Shift+E` | **ECL Format/Minify** | Toggle formatting of Expression Constraint Language expressions |
| `Ctrl+Shift+B` | **Shrimp Browser** | Open the selected concept in the SNOMED CT browser |

All hotkeys are fully customizable via the Settings window.

### Concept Lookup

1. Select a concept ID (e.g., `73211009`) in any application
2. Press **Ctrl+Shift+L** (default hotkey)
3. A popup appears near your cursor showing concept details (FSN, PT, status, edition)

### Concept Search

1. Press **Ctrl+Shift+S** to open the search window
2. Type a search term (e.g., "diabetes")
3. Select the code system and edition to search
4. Click a result to copy or insert it

### Bulk Replace

1. Select text containing concept IDs in any application
2. Press **Ctrl+Shift+R**
3. Concept IDs are annotated with display terms (or terms are removed if already present)

### ECL Format/Minify

1. Select an ECL expression in any application
2. Press **Ctrl+Shift+E**
3. The expression toggles between formatted (indented) and minified forms

### Shrimp Browser

1. Select a SNOMED CT concept ID in any application
2. Press **Ctrl+Shift+B**
3. The concept opens in the Shrimp SNOMED CT browser in your default web browser

### Copy buttons (Lookup popup)

The lookup popup includes five copy buttons:

| Button | What it copies |
|--------|----------------|
| Copy ID | Just the concept ID |
| Copy FSN | The Fully Specified Name |
| Copy PT | The Preferred Term |
| Copy ID & FSN | Format: `ID \| FSN \|` |
| Copy ID & PT | Format: `ID \| PT \|` |

### Dismissing popups

- Press **Escape**
- Click outside the popup
- Click the **X** button

### Changing hotkeys

1. Right-click the tray icon, then **Settings**
2. Click the hotkey recorder for the hotkey you want to change
3. Press your desired key combination
4. Click **Save**

---

## Configuration

### Settings location

Settings are stored in:
```
%LOCALAPPDATA%\AEHRC\Codeagogo\settings.json
```

### Available settings

| Setting | Default | Description |
|---------|---------|-------------|
| Lookup Hotkey | Ctrl+Shift+L | Concept lookup shortcut |
| Search Hotkey | Ctrl+Shift+S | Concept search shortcut |
| Replace Hotkey | Ctrl+Shift+R | Bulk replace shortcut |
| ECL Hotkey | Ctrl+Shift+E | ECL format/minify shortcut |
| Browser Hotkey | Ctrl+Shift+B | Shrimp browser shortcut |
| FHIR Endpoint | https://tx.ontoserver.csiro.au/fhir | Terminology server URL |
| Code Systems | SNOMED CT (default) | Configurable code systems |
| Debug Logging | Off | Enable verbose logging |

### Code systems

Configure which code systems are available for lookup and search. Each code system requires:
- **Name** -- Display name (e.g., "SNOMED CT", "LOINC")
- **System URL** -- FHIR system identifier (e.g., `http://snomed.info/sct`)
- **Version** (optional) -- Specific version to use

### FHIR endpoint

The default endpoint is CSIRO's Ontoserver. You can configure a different FHIR R4 terminology server that supports the `CodeSystem/$lookup` and `ValueSet/$expand` operations.

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
| Application | Where you extracted it (e.g., `Codeagogo.exe`) |
| Settings | `%LOCALAPPDATA%\AEHRC\Codeagogo\settings.json` |
| Logs | `%LOCALAPPDATA%\AEHRC\Codeagogo\logs\app.log` |

---

## Troubleshooting

### Nothing happens when I press a hotkey

1. **Check the app is running** -- Look for the icon in the system tray
2. **Check tray overflow** -- The icon may be hidden in the overflow area (click the ^ arrow)
3. **Verify the hotkey** -- Open Settings to see the configured hotkeys
4. **Check for conflicts** -- Another application may be using the same hotkey

### "No concept ID found" message

- Ensure you have selected text containing a valid concept ID
- For SNOMED CT, the app validates IDs using the Verhoeff check digit algorithm
- Try copying the ID to clipboard manually before pressing the hotkey

### Popup doesn't appear at cursor

- The popup appears near the cursor position when the hotkey was pressed
- Some applications may move focus, affecting cursor position

### Network errors or timeouts

1. **Check internet connectivity**
2. **Verify FHIR endpoint** -- Try the default Ontoserver URL in Settings
3. **Check firewall** -- Ensure the app can make outbound HTTPS connections
4. **Corporate proxy** -- Some networks may block or require proxy configuration

### App doesn't start

1. **Check .NET 8 Runtime** -- Self-contained builds include the runtime, but framework-dependent builds require it
2. **Run as Administrator** -- Try right-click, then Run as administrator
3. **Check antivirus** -- Some antivirus software may block unsigned executables

### Visualization doesn't work

1. **Check WebView2 Runtime** -- Required for concept visualization. Included in Windows 11; may need to be installed on Windows 10
2. Download from: https://developer.microsoft.com/en-us/microsoft-edge/webview2/

### Hotkey stops working

- The hotkey may have been captured by another application
- Restart Codeagogo to re-register the hotkeys
- Try a different hotkey combination in Settings

---

## Running at startup

To start Codeagogo automatically when Windows starts:

1. Press `Win+R`, type `shell:startup`, press Enter
2. Create a shortcut to `Codeagogo.exe` in this folder

---

## Uninstalling

To remove Codeagogo:

1. Right-click the tray icon, then **Exit**
2. Delete the application folder
3. (Optional) Delete settings and logs:
   ```
   %LOCALAPPDATA%\AEHRC\Codeagogo\
   ```
4. (Optional) Remove startup shortcut if created

---

## Support

For issues, questions, or feedback, please contact the project maintainers or raise an issue in the GitHub repository.
