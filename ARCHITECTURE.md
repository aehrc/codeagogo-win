# Codeagogo Architecture (Windows)

Technical architecture of the Windows system tray application for clinical terminology lookup.

## Overview

- **Hybrid UI:** WinForms `NotifyIcon` for system tray, WPF for all windows
- **6 Global Hotkeys:** Lookup, Search, Replace, ECL Format, ECL Evaluate, Browser — all via Win32 `RegisterHotKey`
- **FHIR R4:** `CodeSystem/$lookup` for single lookups, `ValueSet/$expand` for search, batch, and ECL evaluation
- **ECL via ecl-core:** `@aehrc/ecl-core` TypeScript library running in Jint (pure .NET JS interpreter) — shared bundle with macOS sibling

## Data Flow

**Lookup:** Hotkey → `TrayAppContext` → `ClipboardSelectionReader` simulates Ctrl+C (with clipboard save/restore) → extract concept ID → cache check → `OntoserverClient` FHIR query → `PopupWindow` near cursor.

**Replace:** Hotkey → capture selection → two-pass regex extraction (SNOMED numeric codes, then alphanumeric LOINC/ICD-10 codes) → `BatchLookupAsync` against default edition → fallback to per-edition parallel lookups for namespaced SCTIDs → smart toggle (add/remove terms) → paste replacement.

**Search:** Hotkey → `SearchWindow` opens → `SearchViewModel` debounces input → `ValueSet/$expand` → display results → optional insert into source app.

**ECL Format:** Hotkey → capture selection → `ECLBridge.ToggleECLFormat` (ecl-core via Jint) → toggle formatted/minified → paste result.

**ECL Evaluate:** Hotkey → capture selection → validate via `ECLBridge.ParseECL` → `OntoserverClient.EvaluateEclAsync` (ValueSet/$expand with implicit ECL ValueSet URL) → `EvaluateWindow` shows concepts with semantic tags, Shrimp links, diagrams. Background concept validation shows inactive/unknown warnings.

## Concurrency Model

| Component | Thread Model | Notes |
|-----------|-------------|-------|
| TrayAppContext, all WPF windows | UI Thread | Use `Dispatcher.InvokeAsync` from background |
| ClipboardSelectionReader | UI Thread (STA) | Clipboard requires STA thread |
| OntoserverClient | async/await | Non-blocking I/O with retry |
| LruCache | lock-based | Dictionary + linked list need synchronized access |
| ECLBridge (Jint) | Single-threaded | Jint Engine is not thread-safe; lazily initialized |
| VisualizationWindow | Task.Run + ConfigureAwait(false) | Network calls off UI thread to prevent deadlock |

## Caching

| Layer | TTL | Strategy |
|-------|-----|----------|
| Concept cache (`LruCache`) | 6 hours | LRU eviction at 100 entries |
| `OntoserverClient` concept cache | 6 hours | `ConcurrentDictionary` with timestamp |
| Edition list | 30 minutes | Static cache via `GetCachedOrFetchEditionsAsync` |

## Error Handling

| Category | Handling |
|----------|----------|
| Network errors, 5xx | Retry with exponential backoff (500ms, 1s) |
| 4xx client errors | No retry, show error |
| Concept not found | Multi-edition fallback, then error message |
| No text selected | "No ID found" message |
| Invalid ECL | Parse error with beep |
| Invalid SCTID check digit | Treat as non-SNOMED code, try configured systems |

## Win32 APIs

| API | Purpose |
|-----|---------|
| `RegisterHotKey` / `UnregisterHotKey` | 6 global hotkeys |
| `keybd_event` | Ctrl+C simulation (more reliable than SendInput for copy) |
| `SendInput` | Text re-selection after paste (Shift+Arrow) |
| `GetForegroundWindow` / `SetForegroundWindow` | Target window management |
| `AttachThreadInput` | Cross-thread input for clipboard operations |

## Design Decisions

**WinForms tray icon:** WPF has no built-in `NotifyIcon`. WinForms `NotifyIcon` is the standard approach.

**Simulated Ctrl+C:** Windows has no universal API to read selected text across applications. Simulating Ctrl+C and reading the clipboard is the most reliable cross-app method.

**`keybd_event` over `SendInput` for copy:** `keybd_event` proved more reliable across different application types for Ctrl+C simulation. `SendInput` is used for text re-selection where its capabilities are needed.

**FHIR over Snowstorm:** Standardized format, multi-edition support, multi-code system support, `ValueSet/$expand` for search/batch, broader server compatibility.

**Lock-based `LruCache`:** The cache maintains both a dictionary and a linked list in sync. `lock` provides simple, correct synchronization for this compound structure.

**ecl-core via Jint over native C# parser:** The `@aehrc/ecl-core` TypeScript library provides richer formatting (9 options), structured errors, concept extraction, and a 50-article knowledge base. Jint (pure .NET JS interpreter) was chosen over ClearScript/V8 to avoid 30MB native dependencies and preserve single-file deployment. The same bundle is shared with the macOS sibling (JavaScriptCore). Performance is acceptable — parse/format operations are sub-second.

**WebView2 for visualization:** High-quality SVG rendering with built-in zoom/pan and PNG export. Included in Windows 11, available for Windows 10. Falls back to opening diagram HTML in the default browser when WebView2 Runtime is not installed.
