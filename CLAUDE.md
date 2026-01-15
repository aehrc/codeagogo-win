# CLAUDE.md

## Project Overview

Codeagogo is a Windows WPF system tray app for SNOMED CT and multi-terminology lookup. Hotkeys trigger instant clinical terminology lookups by reading selected text from any application.

- **Framework:** .NET 8, WPF windows, WinForms NotifyIcon tray icon
- **Server:** FHIR R4 terminology server (default: CSIRO Ontoserver `https://tx.ontoserver.csiro.au/fhir`)
- **Code systems:** SNOMED CT, LOINC, ICD-10, RxNorm (configurable)
- **Solution:** `Codeagogo.sln`
- **Mac sibling:** `../codeagogo` — same features in Swift/macOS. Defect fixes may need porting between them.

## Build & Test

```bash
dotnet build                                                    # Build (warnings are errors in Release)
dotnet test --filter "Category!=E2E&Category!=Integration"      # Unit tests only
dotnet test tests/Codeagogo.E2ETests --filter "Category=E2E"   # E2E tests (requires desktop session)
dotnet run --project src/Codeagogo                              # Run the app
```

## Architecture

**Data flow:** Hotkey press → `TrayAppContext` orchestrates → `ClipboardSelectionReader` captures selection via simulated Ctrl+C → concept ID extraction → `OntoserverClient` FHIR lookup → result displayed in `PopupWindow`.

Key interactions that aren't obvious from the code:
- `TrayAppContext` is the central coordinator — all hotkey handlers live here, and it owns the single shared `OntoserverClient` instance (important for cache persistence across invocations)
- `ClipboardSelectionReader.ExtractAllConceptIds` uses a **two-pass regex**: first SNOMED numeric codes (6-18 digits), then alphanumeric codes (LOINC, ICD-10) — order matters to avoid overlaps
- `OntoserverClient.BatchLookupAsync` tries the default SNOMED edition first, then falls back to per-edition parallel lookups for namespaced SCTIDs. `SCTIDValidator.IsCoreSCTID` skips the fallback for International Edition codes
- Edition list is cached with 30-min TTL (`GetCachedOrFetchEditionsAsync`)
- `ECLBridge` wraps the `@aehrc/ecl-core` TypeScript library running in Jint (pure .NET JS interpreter). The `ecl-core-bundle.js` is an embedded resource shared with the Mac sibling. Lazily initialized on first ECL operation
- `EvaluateEclAsync` uses `ValueSet/$expand` with an implicit ECL ValueSet URL (`http://snomed.info/sct?fhir_vs=ecl/<expression>`)
- **Diagram threading:** All network calls in `VisualizationWindow.LoadDiagramAsync` run via `Task.Run` with `ConfigureAwait(false)` to avoid WPF SynchronizationContext deadlocks. Normal form parsing has a 5-second timeout for complex product concepts. Falls back to browser if WebView2 is unavailable
- **Diagram display terms:** Use `LookupDefaultEditionAsync` (no version) for server-default edition PTs (e.g., "Ampoule" from SCTAU not "Ampule" from International). FHIR subproperties provide pre-coordinated concept IDs (e.g., 258798001 "mg/mL") to replace normal form decomposed expressions
- **Selection capture:** Uses FlaUI COM UIA3 as primary strategy (fast, no clipboard disruption), falls back to Ctrl+C simulation for browsers. The managed `System.Windows.Automation` wrapper is NOT safe (causes `AccessViolationException`)
- **Auto-update:** Velopack checks GitHub Releases on startup and every 24h. Custom `Main` entry point required for Velopack hooks. App.xaml build action is `Page` not `ApplicationDefinition`

## Project Structure

```
src/Codeagogo/              # Main WPF application
src/Codeagogo/Visualization/ # SNOMED CT concept diagram SVG rendering (WebView2 or browser fallback)
src/Codeagogo/Controls/     # Reusable WPF controls (HotKeyRecorder)
scripts/                    # ecl-core bundle build tooling (npm + esbuild)
tests/Codeagogo.Tests/      # Unit tests
tests/Codeagogo.E2ETests/   # E2E tests (FlaUI-based, requires desktop)
```

## Code Style

- `.editorconfig` enforced at build time (`EnforceCodeStyleInBuild=true`)
- Allman braces, file-scoped namespaces, `var` for obvious types
- PascalCase public members, `_camelCase` private fields, `I`-prefix interfaces
- Prefer `async`/`await` over `.Result`
- XML doc comments on all public types and members

## Development Rules

- Run `dotnet test` before committing
- All HTTP requests go through `OntoserverClient` (not direct `HttpClient`)
- Settings persist to `%LOCALAPPDATA%\AEHRC\Codeagogo\`
- Logs: `%LOCALAPPDATA%\AEHRC\Codeagogo\logs\app.log`

## Gotchas

- **Threading:** WPF Clipboard ops must be on STA thread. Use `Dispatcher.InvokeAsync` for UI from background threads. WPF windows must be created on UI thread.
- **Hotkeys:** IDs must be unique per window handle. Register/unregister on the message loop thread.
- **Win32:** `SendInput` requires target window in foreground. `ClipboardSelectionReader` saves/restores clipboard to avoid disrupting user.
- **WebView2:** Requires async initialization — do not block on it. Falls back to opening diagram in browser if runtime is unavailable.
- **ECL Bridge:** Jint Engine is not thread-safe — all calls must be on the same thread. The `_eclBridge` in `TrayAppContext` is `Lazy<>` to defer the ~2MB bundle load until first ECL operation.
- **Diagram normal form:** Australian CTPP concepts have deeply nested compositional grammar that can hang `NormalFormParser.Parse`. A 5-second timeout protects against this.
