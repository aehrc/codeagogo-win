# Contributing to SNOMED Lookup (Windows)

Thank you for your interest in contributing to SNOMED Lookup! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [Making Changes](#making-changes)
- [Code Style](#code-style)
- [Testing](#testing)
- [Submitting Changes](#submitting-changes)
- [Reporting Issues](#reporting-issues)

## Code of Conduct

Please be respectful and constructive in all interactions. We aim to maintain a welcoming and inclusive environment for all contributors.

## Getting Started

### Prerequisites

- Windows 10 or 11
- .NET 8 SDK or Visual Studio 2022 with ".NET desktop development" workload
- Git
- Basic familiarity with C#, WPF, and Windows development

### Understanding the Project

Before contributing, familiarize yourself with:

1. **[README.md](README.md)** — Project overview and features
2. **[ARCHITECTURE.md](ARCHITECTURE.md)** — Technical design and component responsibilities
3. **[PRIVACY.md](PRIVACY.md)** — Privacy considerations and constraints

## Development Setup

### 1. Fork and Clone

```powershell
# Fork the repository on GitHub, then clone your fork
git clone https://github.com/YOUR_USERNAME/snomed-lookup-win.git
cd snomed-lookup-win
```

### 2. Open in Visual Studio

```powershell
# Open the solution
start SNOMEDLookup.sln

# Or use VS Code with C# extension
code .
```

### 3. Build and Run

```powershell
# Build the project
dotnet build

# Run the application
dotnet run --project src/SNOMEDLookup

# Run tests
dotnet test
```

### 4. Verify Setup

Ensure:
- The application builds without errors
- All tests pass
- The app runs and shows a tray icon
- Hotkey lookup works correctly

## Making Changes

### Branch Naming

Use descriptive branch names:

- `feature/` — New features (e.g., `feature/batch-lookup`)
- `fix/` — Bug fixes (e.g., `fix/cache-expiration`)
- `docs/` — Documentation changes (e.g., `docs/api-reference`)
- `refactor/` — Code refactoring (e.g., `refactor/extract-fhir-parser`)
- `test/` — Test additions or improvements (e.g., `test/edge-cases`)

### Commit Messages

Write clear, descriptive commit messages:

```
<type>: <short summary>

<optional longer description>

<optional footer>
```

**Types:**
- `feat` — New feature
- `fix` — Bug fix
- `docs` — Documentation
- `refactor` — Code refactoring
- `test` — Test changes
- `chore` — Build, CI, or tooling changes

**Examples:**
```
feat: add batch concept lookup support

Allows users to look up multiple concept IDs at once by selecting
a comma-separated list. Results are displayed in a scrollable list.

Closes #42
```

```
fix: correct cache TTL calculation

The cache was using creation time instead of last access time for
TTL checks, causing premature cache misses.
```

## Code Style

### C# Conventions

Follow the [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):

- Use clear, descriptive names
- Prefer clarity over brevity
- Use PascalCase for public members
- Use camelCase with underscore prefix for private fields (`_privateField`)
- Use `var` when the type is obvious from the right side

### Documentation

Add XML documentation comments for:

- All public types, methods, and properties
- Complex private implementations
- Non-obvious logic

```csharp
/// <summary>
/// Looks up a SNOMED CT concept by its identifier.
/// </summary>
/// <remarks>
/// This method first checks the cache, then queries the FHIR server
/// if the concept is not cached or has expired.
/// </remarks>
/// <param name="conceptId">The SNOMED CT concept identifier (6-18 digits)</param>
/// <returns>The concept result containing FSN, PT, and status</returns>
/// <exception cref="ConceptNotFoundException">
/// Thrown when the concept doesn't exist in any available edition
/// </exception>
public async Task<ConceptResult> LookupAsync(string conceptId)
```

### Code Organization

Organize code with regions or comments:

```csharp
// === Fields ===

private readonly HttpClient _httpClient;
private readonly LruCache<string, ConceptResult> _cache;

// === Constructor ===

public FhirClient(string baseUrl) { ... }

// === Public Methods ===

public async Task<ConceptResult> LookupAsync(string conceptId) { ... }

// === Private Methods ===

private async Task<T> GetJsonWithRetryAsync<T>(string url) { ... }
```

### WPF Best Practices

- Use MVVM patterns where appropriate
- Bind to view models rather than code-behind logic
- Use resource dictionaries for shared styles
- Keep XAML clean and well-formatted

### Error Handling

- Use specific exception types where possible
- Provide descriptive error messages
- Log errors with appropriate context
- Handle errors gracefully in the UI

```csharp
try
{
    var result = await _client.LookupAsync(conceptId);
    ShowResult(result);
}
catch (HttpRequestException ex)
{
    Log.Error($"Network error looking up {conceptId}: {ex.Message}");
    ShowError("Network error. Please check your connection.");
}
catch (Exception ex)
{
    Log.Error($"Unexpected error: {ex}");
    ShowError("An unexpected error occurred.");
}
```

## Testing

### Running Tests

```powershell
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~LruCacheTests"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Writing Tests

- Add tests for new functionality
- Update tests when modifying existing behavior
- Use descriptive test method names

```csharp
[Fact]
public void TryGet_ReturnsValue_WhenKeyExistsAndNotExpired()
{
    // Arrange
    var cache = new LruCache<string, string>();
    cache.Set("key1", "value1");

    // Act
    var found = cache.TryGet("key1", out var value);

    // Assert
    Assert.True(found);
    Assert.Equal("value1", value);
}
```

### Test Categories

| Category | Purpose |
|----------|---------|
| Unit Tests | Test individual functions and types in isolation |
| Integration Tests | Test component interactions |

### Code Coverage

Aim for meaningful coverage of:
- Core business logic
- Error handling paths
- Edge cases
- Cache behavior

## Submitting Changes

### Before Submitting

1. **Ensure all tests pass**
   ```powershell
   dotnet test
   ```

2. **Verify no compiler warnings**
   ```powershell
   dotnet build --warnaserror
   ```

3. **Update documentation** if needed
   - README.md for user-facing changes
   - ARCHITECTURE.md for design changes
   - CHANGELOG.md for all notable changes

4. **Add or update tests** for your changes

### Pull Request Process

1. **Create a pull request** from your feature branch to `main`

2. **Fill out the PR template** with:
   - Description of changes
   - Related issue numbers
   - Testing performed
   - Screenshots (for UI changes)

3. **Address review feedback** promptly

4. **Squash commits** if requested, keeping a clean history

### PR Title Format

Use the same format as commit messages:
```
feat: add batch concept lookup support
fix: correct cache TTL calculation
docs: update API documentation
```

## Reporting Issues

### Bug Reports

Include:
- Windows version
- .NET version (if not using self-contained build)
- App version
- Steps to reproduce
- Expected vs actual behavior
- Relevant logs (use Export Diagnostics in Settings)

### Feature Requests

Include:
- Use case description
- Proposed solution
- Alternative approaches considered
- Impact on existing functionality

### Security Issues

For security vulnerabilities, please contact the maintainers directly rather than opening a public issue.

## Questions?

If you have questions about contributing, feel free to:
- Open a discussion on GitHub
- Review existing issues and pull requests
- Check the documentation

Thank you for contributing to SNOMED Lookup!
