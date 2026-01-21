# SNOMED Lookup Architecture (Windows)

This document describes the technical architecture of SNOMED Lookup, a Windows system tray application for looking up SNOMED CT concepts.

## Table of Contents

- [Overview](#overview)
- [System Architecture](#system-architecture)
- [Component Details](#component-details)
- [Data Flow](#data-flow)
- [Concurrency Model](#concurrency-model)
- [Caching Strategy](#caching-strategy)
- [Error Handling](#error-handling)
- [Security Considerations](#security-considerations)
- [Dependencies](#dependencies)
- [Design Decisions](#design-decisions)

## Overview

SNOMED Lookup is a lightweight Windows utility that enables users to look up SNOMED CT (Systematized Nomenclature of Medicine - Clinical Terms) concepts from any application using a global hotkey.

### Key Characteristics

- **System Tray Application** — Runs as a background process with notification area presence
- **Global Hotkey** — Responds to system-wide keyboard shortcuts via Win32 API
- **FHIR Integration** — Queries FHIR R4 terminology servers
- **Hybrid UI Framework** — WinForms for tray icon, WPF for windows
- **Thread-Safe Caching** — Lock-based concurrency for safe parallel operations

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SNOMED Lookup Application                        │
├─────────────────────────────────────────────────────────────────────┤
│  Presentation Layer                                                 │
│  ┌───────────────┐  ┌─────────────────┐  ┌─────────────────────┐   │
│  │ NotifyIcon    │  │ PopupWindow     │  │ SettingsWindow      │   │
│  │ (WinForms)    │  │ (WPF)           │  │ (WPF)               │   │
│  │               │  │                 │  │                     │   │
│  │ - Tray icon   │  │ - Results view  │  │ - Hotkey config     │   │
│  │ - Context menu│  │ - Copy buttons  │  │ - FHIR endpoint     │   │
│  └───────────────┘  │ - Close button  │  │ - Logging options   │   │
│                     └─────────────────┘  └─────────────────────┘   │
├─────────────────────────────────────────────────────────────────────┤
│  Application Layer                                                  │
│  ┌───────────────────────────────────────────────────────────────┐ │
│  │ TrayAppContext                                                 │ │
│  │ - Coordinates all components                                   │ │
│  │ - Manages hotkey registration                                  │ │
│  │ - Handles lookup workflow                                      │ │
│  └───────────────────────────────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────────────┤
│  Service Layer                                                      │
│  ┌─────────────────┐  ┌─────────────────┐  ┌───────────────────┐   │
│  │ FhirClient      │  │ Selection       │  │ LruCache          │   │
│  │                 │  │ Reader          │  │                   │   │
│  │ - FHIR queries  │  │                 │  │ - TTL expiration  │   │
│  │ - Response parse│  │ - Clipboard ops │  │ - LRU eviction    │   │
│  │ - Multi-edition │  │ - SendInput     │  │ - Thread-safe     │   │
│  │ - Retry logic   │  │ - Restore       │  │                   │   │
│  └─────────────────┘  └─────────────────┘  └───────────────────┘   │
├─────────────────────────────────────────────────────────────────────┤
│  Infrastructure Layer                                               │
│  ┌─────────────────┐  ┌─────────────────┐  ┌───────────────────┐   │
│  │ Settings        │  │ Log             │  │ Models            │   │
│  │                 │  │                 │  │                   │   │
│  │ - JSON persist  │  │ - File logging  │  │ - ConceptResult   │   │
│  │ - User prefs    │  │ - Debug toggle  │  │ - EditionNames    │   │
│  └─────────────────┘  └─────────────────┘  └───────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│  External: FHIR Terminology Server (Ontoserver) via HTTPS          │
└─────────────────────────────────────────────────────────────────────┘
```

## Component Details

### Presentation Layer

#### `App.xaml.cs`
- **Role**: WPF Application entry point
- **Responsibilities**:
  - Initialize WPF application
  - Create and manage TrayAppContext
  - Handle application lifecycle

#### `PopupWindow.xaml(.cs)`
- **Role**: Results display window
- **Responsibilities**:
  - Display concept lookup results
  - Show loading and error states
  - Provide copy-to-clipboard buttons
  - Handle dismiss actions (Escape, click-outside, X button)
  - Position window near cursor

#### `SettingsWindow.xaml(.cs)`
- **Role**: Application preferences UI
- **Responsibilities**:
  - Configure global hotkey
  - Configure FHIR endpoint URL
  - Toggle debug logging
  - Export diagnostic information

### Application Layer

#### `TrayAppContext.cs`
- **Role**: Main application coordinator
- **Responsibilities**:
  - Create and manage NotifyIcon (system tray)
  - Register global hotkey via Win32 API
  - Coordinate lookup workflow
  - Manage popup window lifecycle
  - Handle settings changes
- **Key APIs**:
  - `RegisterHotKey` / `UnregisterHotKey` for global hotkey
  - `WndProc` message loop for hotkey events

### Service Layer

#### `FhirClient.cs`
- **Role**: FHIR terminology server client
- **Responsibilities**:
  - Query FHIR `CodeSystem/$lookup` endpoint
  - Fetch available SNOMED CT editions
  - Parse FHIR Parameters responses
  - Manage in-memory cache
  - Handle multi-edition fallback
  - Implement retry with exponential backoff
- **FHIR Operations**:
  - `GET /CodeSystem/$lookup?system=...&version=...&code=...`
  - `GET /CodeSystem?url=http://snomed.info/sct,http://snomed.info/xsct`

#### `ClipboardSelectionReader.cs`
- **Role**: System text selection capture
- **Responsibilities**:
  - Snapshot current clipboard contents
  - Simulate Ctrl+C via `keybd_event` P/Invoke
  - Read copied text from clipboard
  - Restore original clipboard contents
- **Key APIs**:
  - `keybd_event` for keyboard simulation
  - `Clipboard` for clipboard operations

#### `LruCache.cs`
- **Role**: Thread-safe result cache
- **Responsibilities**:
  - Store lookup results with timestamps
  - Implement TTL-based expiration
  - Implement LRU eviction at capacity
  - Thread-safe via `lock` statements

### Infrastructure Layer

#### `Settings.cs`
- **Role**: Persisted user settings
- **Responsibilities**:
  - Load/save settings to JSON file
  - Provide default values
  - Store hotkey configuration
  - Store FHIR endpoint URL
  - Store logging preferences

#### `Log.cs`
- **Role**: Application logging
- **Responsibilities**:
  - Write timestamped log entries
  - Support debug/info/error levels
  - Toggle debug logging
  - Provide log retrieval for diagnostics
  - Truncate strings for logging

#### `Models.cs`
- **Role**: Data transfer objects
- **Responsibilities**:
  - Define `ConceptResult` record
  - Map module IDs to edition names

### Data Models

#### `ConceptResult`
```csharp
public sealed record ConceptResult(
    string ConceptId,      // SNOMED CT identifier
    string Branch,         // Edition/branch identifier
    string? Fsn,           // Fully Specified Name
    string? Pt,            // Preferred Term
    bool? Active,          // Active/inactive status
    string? EffectiveTime, // Version date
    string? ModuleId,      // Module identifier
    string? Edition = null // Human-readable edition name
);
```

#### `SnomedEdition`
```csharp
public class SnomedEdition
{
    public string System { get; set; }   // "http://snomed.info/sct" or "xsct"
    public string Version { get; set; }  // Edition URI
    public string Title { get; set; }    // Human-readable name
}
```

## Data Flow

### Lookup Flow

```
┌─────────────────┐
│ User selects    │
│ text in any app │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ User presses    │
│ hotkey          │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ WndProc receives│
│ WM_HOTKEY       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Selection Reader│
│ captures text   │
│ (Ctrl+C sim)    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Extract concept │
│ ID from text    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌─────────────┐
│ Check cache     │────▶│ Cache hit?  │
└─────────────────┘     └──────┬──────┘
                               │
              ┌────────────────┴────────────────┐
              │ Yes                             │ No
              ▼                                 ▼
     ┌─────────────────┐              ┌─────────────────┐
     │ Return cached   │              │ Query           │
     │ result          │              │ International   │
     └────────┬────────┘              │ Edition first   │
              │                       └────────┬────────┘
              │                                │
              │                       ┌────────┴────────┐
              │                       │ Found?          │
              │                       └────────┬────────┘
              │              ┌─────────────────┴─────────────────┐
              │              │ Yes                               │ No
              │              ▼                                   ▼
              │     ┌─────────────────┐              ┌─────────────────┐
              │     │ Cache & return  │              │ Parallel search │
              │     └────────┬────────┘              │ all editions    │
              │              │                       └────────┬────────┘
              │              │                                │
              │              │                       ┌────────┴────────┐
              │              │                       │ First match     │
              │              │                       └────────┬────────┘
              │              │                                │
              ▼              ▼                                ▼
     ┌─────────────────────────────────────────────────────────────┐
     │ Show popup with result                                      │
     └─────────────────────────────────────────────────────────────┘
```

## Concurrency Model

### Threading Strategy

| Component | Thread Model | Reason |
|-----------|-------------|--------|
| `TrayAppContext` | UI Thread | Windows message loop |
| `PopupWindow` | UI Thread | WPF dispatcher |
| `FhirClient` | async/await | Non-blocking I/O |
| `LruCache` | lock-based | Thread-safe data access |
| `SelectionReader` | UI Thread | Clipboard access |

### Parallel Operations

Edition lookups use `Task.WhenAny` for parallel execution with early exit:

```csharp
var tasks = editions.Select(edition =>
    LookupInSystemAsync(conceptId, edition.System, edition.Version)
).ToList();

while (tasks.Count > 0)
{
    var completed = await Task.WhenAny(tasks);
    tasks.Remove(completed);

    var result = await completed;
    if (result != null)
    {
        // Cancel and return first successful result
        return result;
    }
}
```

## Caching Strategy

### Cache Properties

| Property | Value | Rationale |
|----------|-------|-----------|
| **Type** | In-memory (lock-based) | Thread-safe, no persistence needed |
| **TTL** | 6 hours | Balance freshness vs. API load |
| **Max Size** | 100 entries | Limit memory usage |
| **Eviction** | LRU (Least Recently Used) | Keep frequently accessed concepts |

### Cache Entry Structure

```csharp
private sealed class CacheEntry
{
    public TKey Key { get; init; }
    public TValue Value { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}
```

### Cache Operations

- **Get**: Check TTL, move to front (MRU), return result
- **Set**: Evict LRU if at capacity, store with timestamp
- **Eviction**: Remove entry from end of linked list

## Error Handling

### Error Categories

| Category | Examples | Handling |
|----------|----------|----------|
| Network | Timeout, DNS failure | Retry with backoff |
| Server | 5xx errors | Retry with backoff |
| Client | 4xx errors | No retry, show error |
| Not Found | Concept doesn't exist | Show "not found" message |
| Selection | No text selected | Show "no ID found" message |

### Retry Strategy

For transient network failures:

| Attempt | Delay | Total Wait |
|---------|-------|------------|
| 1 | 0s | 0s |
| 2 | 500ms | 500ms |
| 3 | 1000ms | 1500ms |

Retryable conditions:
- `HttpRequestException` (network errors)
- `TaskCanceledException` (timeouts)
- HTTP 5xx server errors

Non-retryable conditions:
- HTTP 4xx client errors
- Concept not found responses

## Security Considerations

### Permissions

| Permission | Purpose | Scope |
|------------|---------|-------|
| Keyboard (SendInput) | Simulate Ctrl+C | On-demand only |
| Clipboard | Read/restore contents | On-demand only |
| Network (Outgoing) | FHIR API queries | HTTPS only |

### Data Handling

- **No persistent storage** of user data (only settings)
- **Clipboard restoration** after reading
- **HTTPS-only** network communication
- **No telemetry** or analytics
- **No keylogging** — only responds to hotkey events

### Privacy

- Selected text is only read when the user explicitly triggers a lookup
- Concept IDs are sent to the FHIR server (no personal data)
- Cache is cleared on app termination

## Dependencies

### System Frameworks

| Framework | Usage |
|-----------|-------|
| WPF | Popup and settings windows |
| WinForms | System tray NotifyIcon |
| System.Text.Json | JSON serialization |
| System.Net.Http | HTTP client |

### Win32 APIs

| API | Usage |
|-----|-------|
| `RegisterHotKey` | Global hotkey registration |
| `UnregisterHotKey` | Hotkey cleanup |
| `keybd_event` | Keyboard simulation |
| `GetCursorPos` | Cursor position for popup |

### External Services

| Service | Purpose | Endpoint |
|---------|---------|----------|
| CSIRO Ontoserver | FHIR terminology server | `https://tx.ontoserver.csiro.au/fhir` |

## Design Decisions

### Why WinForms for Tray Icon?

WPF doesn't have built-in support for system tray icons. WinForms `NotifyIcon` is the standard approach and integrates well with WPF via `WindowsFormsHost` or hybrid application patterns.

### Why Simulated Ctrl+C for Selection?

Windows doesn't provide a universal API to read selected text across applications. Simulating Ctrl+C and reading the clipboard is the most reliable cross-application method, similar to how tools like Ditto and ClipX work.

### Why `keybd_event` Instead of `SendInput`?

While `SendInput` is the newer API, `keybd_event` proved more reliable across different application types in testing. It's simpler and handles the basic Ctrl+C simulation well.

### Why FHIR Instead of Direct Snowstorm API?

FHIR provides:
- Standardized response format
- Multi-edition support in a single endpoint
- Broader compatibility with terminology servers
- Better long-term maintainability

### Why Lock-Based Cache Instead of Concurrent Collections?

The `LruCache` needs to maintain both a dictionary (for fast lookup) and a linked list (for LRU ordering) in sync. Using `lock` provides simple, correct synchronization for this compound data structure.

### Why Not Use HwndSource for Hotkey?

While `HwndSource.AddHook` works for WPF windows, creating a dedicated message-only window in `TrayAppContext` provides better separation of concerns and works even when no WPF windows are visible.
