# Support

Thanks for your interest in Dataverse Debugger! This project is community-supported. Please use the channels below to get help.

## Self-Service Resources
- **README:** [readme.md](readme.md) covers prerequisites, build instructions, and troubleshooting tips.
- **Architecture Guide:** [ARCHITECTURE.md](ARCHITECTURE.md) explains the host/runner layout and IPC contracts.
- **Changelog:** [CHANGELOG.md](CHANGELOG.md) lists notable updates per release.

## Asking Questions
1. Search existing GitHub Issues to avoid duplicates.
2. If no issue exists, open a new issue with:
   - What you were trying to do.
   - Steps to reproduce.
   - Logs or screenshots.
   - Whether you were running a release build or latest `main`.

## Filing Bugs
Follow the [Issue Reporting Checklist](CONTRIBUTING.md#issue-reporting-checklist). Attach runner logs (`DataverseDebugger.Runner.log`) or CI output where relevant.

## Feature Requests
Create an issue tagged `enhancement` describing the scenario, why it’s useful, and any proposed UI/API changes.

## Security Issues
Do **not** file security bugs publicly. Follow the [Security Policy](SECURITY.md).

## Releases & Builds
- Tagged releases publish signed zip archives via the GitHub Actions workflow defined in [.github/workflows/build-and-release.yml](.github/workflows/build-and-release.yml).
- Nightly or experimental builds must be produced locally using `dotnet publish`.

If you need dedicated support, consider forking the project and tailoring it to your environment. Contributions that improve diagnostics and documentation are always welcome.