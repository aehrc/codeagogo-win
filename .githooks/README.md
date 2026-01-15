# Git Hooks

This directory contains git hooks for the Codeagogo project.

## Setup

To enable these hooks, run the following command from the repository root:

```bash
git config core.hooksPath .githooks
```

This tells git to use the `.githooks/` directory instead of the default `.git/hooks/`.

## Hooks

### pre-commit

Runs automatically before each commit. Checks for:

- **Secret scanning**: Detects potential secrets in staged changes (passwords, API keys, private keys, certificates)
- **Sensitive files**: Warns if `.pfx`, `.p12`, `.pem`, `.key`, or `.env` files are staged
- **Code formatting**: Runs `dotnet format --verify-no-changes` on staged C# files (if available)

The hook will block the commit if potential secrets or sensitive files are detected. Use `git commit --no-verify` to bypass if the detections are false positives.

### pre-push

Runs automatically before each push. Performs:

- **Build validation**: Runs `dotnet build` to ensure the solution compiles
- **Unit tests**: Runs all unit tests (excluding E2E and Integration tests)

The hook will block the push if the build fails or any unit test fails.
