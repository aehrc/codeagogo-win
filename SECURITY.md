# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| Latest  | Yes                |
| Older   | No                 |

Only the latest release receives security updates.

## Reporting a Vulnerability

If you discover a security vulnerability in Codeagogo, please report it responsibly.

**Do not open a public GitHub issue for security vulnerabilities.**

Instead, please email the project maintainers or use [GitHub's private vulnerability reporting](https://github.com/aehrc/codeagogo-win/security/advisories/new) to submit a report.

### What to include

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

### What to expect

- Acknowledgement within 5 business days
- An assessment of the issue within 10 business days
- A fix or mitigation plan for confirmed vulnerabilities
- Credit in the release notes (unless you prefer to remain anonymous)

## Security Practices

This project follows these security practices:

- **Dependency scanning**: NuGet vulnerability audits run on every build
- **Static analysis**: CodeQL SAST scanning on every push and weekly
- **Secret scanning**: Pre-commit hooks check for accidentally committed secrets
- **Signed releases**: Release builds support Authenticode code signing
- **HTTPS enforcement**: Warnings are shown for insecure FHIR server connections
- **Input validation**: URL scheme validation, input length limits, XML escaping
