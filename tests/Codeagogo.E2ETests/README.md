# Codeagogo E2E Tests

End-to-end tests using [FlaUI](https://github.com/FlaUI/FlaUI) - a .NET UI Automation library for Windows desktop applications.

## Prerequisites

- Windows desktop environment (cannot run headless/CI without a desktop session)
- The main application must be built first: `dotnet build src/Codeagogo/Codeagogo.csproj`
- No other instance of Codeagogo should be running

## Running E2E Tests

```bash
# Run only E2E tests
dotnet test tests/Codeagogo.E2ETests --filter "Category=E2E"

# Run all tests (unit + E2E)
dotnet test
```

## Test Architecture

- **FlaUI.UIA3**: Uses Microsoft UI Automation v3 (best for WPF apps)
- **xUnit**: Test framework (consistent with unit tests)
- **Xunit.StaFact**: Enables STA thread tests required for WPF UI interaction
- **FluentAssertions**: Assertion library (consistent with unit tests)

## Test Categories

- `AppLaunchTests`: Verifies the application starts, runs as tray app, doesn't crash
- `SettingsWindowTests`: Verifies the Settings dialog UI elements

## Extending Tests

To add more comprehensive E2E tests:

1. **Tray Icon Interaction**: Use FlaUI to find the system tray icon and interact with it
2. **Hotkey Simulation**: Use `SendInput` API to trigger global hotkeys
3. **Window Verification**: Use FlaUI to find and inspect popup/search/settings windows
4. **Clipboard Integration**: Set clipboard content and verify lookup results

### Example: Finding a Window

```csharp
using var automation = new UIA3Automation();
var window = _app.GetMainWindow(automation, TimeSpan.FromSeconds(5));
var button = window.FindFirstDescendant(cf => cf.ByName("Save"));
button.Click();
```

### Example: System Tray Interaction

```csharp
// System tray interaction requires finding the notification area
var desktop = automation.GetDesktop();
var tray = desktop.FindFirstDescendant(cf => cf.ByClassName("Shell_TrayWnd"));
// Navigate to notification area and find the Codeagogo icon
```
