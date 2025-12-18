# Contributing to Dataverse Debugger

We welcome community contributions! This guide explains how to get started, report issues, and submit pull requests.

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [Ways to Contribute](#ways-to-contribute)
- [Development Workflow](#development-workflow)
- [Commit & PR Guidelines](#commit--pr-guidelines)
- [Issue Reporting Checklist](#issue-reporting-checklist)

## Code of Conduct
Participation in this project is governed by the [Code of Conduct](CODE_OF_CONDUCT.md). Please review it before contributing.

## Ways to Contribute
- **Bug reports:** Describe reproducible issues in the GitHub Issues tracker.
- **Feature suggestions:** Outline the problem you want to solve and the proposed user experience.
- **Documentation:** Improve the README, architecture notes, or add troubleshooting tips.
- **Code fixes:** Tackle issues tagged `good first issue` or propose your own enhancements via an issue before opening a PR.

## Development Workflow
1. **Clone and restore dependencies**
   ```powershell
   git clone https://github.com/<your-account>/DataverseDebugger.git
   cd DataverseDebugger
   nuget restore DataverseDebugger.sln
   dotnet restore DataverseDebugger.sln
   ```

2. **Build and test locally**
   ```powershell
   dotnet build DataverseDebugger.sln -c Debug
   dotnet test DataverseDebugger.Tests/DataverseDebugger.Tests.csproj -c Debug
   ```

3. **Run the app**
   ```powershell
   dotnet run --project DataverseDebugger.App/DataverseDebugger.App.csproj -c Debug
   ```

4. **Publishing for validation**
   ```powershell
   dotnet publish DataverseDebugger.App/DataverseDebugger.App.csproj -c Release
   ```

## Commit & PR Guidelines
- Create a topic branch from `main` (e.g., `feature/restbuilder-hotfix`).
- Keep commits focused and descriptive. Reference GitHub issues with `Fixes #123` where possible.
- Maintain existing coding styles (C# analyzers, nullable warnings, etc.).
- Update/extend tests for new behavior.
- Run `dotnet build DataverseDebugger.sln -c Release` before opening a PR to catch nullable or packaging regressions.
- Document user-facing changes in [CHANGELOG.md](CHANGELOG.md).

When opening a PR:
- Fill out the template (if provided) with context, screenshots/logs, and testing evidence.
- Mention breaking changes explicitly.
- Expect automated GitHub Actions builds (Build and Test + Release) to run; ensure they pass before requesting review.

## Issue Reporting Checklist
When filing a bug, include:
- OS version (Windows build), .NET SDK version, and whether you're running published bits or local builds.
- Exact steps to reproduce.
- Expected vs. actual behavior (include stack traces, logs, or screenshots where possible).
- Whether the issue impacts the runner, host app, REST Builder package, or build pipeline.

Thanks for helping improve Dataverse Debugger!