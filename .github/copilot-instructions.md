# Dataverse Debugger – AI Guide

## Architecture
- Modern WPF host lives in [DataverseDebugger.App](DataverseDebugger.App) and targets `net8.0-windows`; it embeds WebView2, drives authentication, and owns request capture/routing (see [DataverseDebugger.App/DataverseDebugger.App.csproj](DataverseDebugger.App/DataverseDebugger.App.csproj)).
- The isolated plugin runner is a `net48` console app in [DataverseDebugger.Runner](DataverseDebugger.Runner); it hosts the pipeline, loads assemblies via shadow copies, and surfaces telemetry over named pipes.
- Shared DTOs and pipe helpers sit in [DataverseDebugger.Protocol](DataverseDebugger.Protocol); update these types before touching either endpoint so command payloads stay in sync.
- `RunnerProcessManager` in [DataverseDebugger.App/RunnerProcessManager.cs](DataverseDebugger.App/RunnerProcessManager.cs) starts the runner, passes `DATAVERSE_DEBUGGER_HOST_PID`, and falls back to sibling `DataverseDebugger.Runner/bin/<Configuration>/net48` outputs when a bundled copy is missing.
- IPC happens through `PipeNames.RunnerPipe`; the host side client is [DataverseDebugger.App/RunnerClient.cs](DataverseDebugger.App/RunnerClient.cs) and the server loop is [DataverseDebugger.Runner/RunnerPipeServer.cs](DataverseDebugger.Runner/RunnerPipeServer.cs).

## Build & Run
- Prereqs: .NET 8 SDK + .NET Framework 4.8 Developer Pack, WebView2 runtime, VS 2022 (optional for debugging) as listed in [readme.md](readme.md).
- Typical local loop: `dotnet build DataverseDebugger.sln -c Debug`, then `dotnet run --project DataverseDebugger.App -c Debug`. The runner binary is built alongside and auto-spawned by the host.
- To exercise tests run `dotnet test DataverseDebugger.Tests/DataverseDebugger.Tests.csproj`; current tests are MSTest stubs in [DataverseDebugger.Tests/Test1.cs](DataverseDebugger.Tests/Test1.cs), so add coverage where it matters (routing, `$batch`, metadata parsing).
- When the host cannot find `runner/DataverseDebugger.Runner.exe`, ensure `DataverseDebugger.Runner` was built in the same configuration or adjust `RunnerProcessManager.ResolveRunnerPath`.

## Runner & IPC Contracts
- Runner commands: `health`, `initWorkspace`, `execute`, `executePlugin`, `runnerLogConfig`, and `runnerLogFetch` (see [DataverseDebugger.Runner/RunnerPipeServer.cs](DataverseDebugger.Runner/RunnerPipeServer.cs)). Respect these names when adding features; unknown commands return an error envelope.
- `RunnerClient.ExecuteAsync` streams trace deltas via `executeTrace` messages before sending the final `executeResponse`. Preserve this pattern so the UI can surface live traces.
- Workspace init payload (`InitializeWorkspaceRequest`) carries assemblies, symbol paths, metadata caches, and environment flags; keep additions backwards compatible via `CapabilityFlags` in [DataverseDebugger.Protocol](DataverseDebugger.Protocol).

## Request Routing & Execution Flow
- The host intercepts HTTP via WebView2 DevTools, classifies requests, and either proxies them to Dataverse or emulates them through the runner; the rules are spelled out in [ARCHITECTURE.md](ARCHITECTURE.md) and mirrored in the routing services under [DataverseDebugger.App](DataverseDebugger.App).
- `$batch` support, impersonation, and request metadata mapping rely on OData helpers plus conversion utilities inside [DataverseDebugger.Runner.Conversion](DataverseDebugger.Runner.Conversion); keep metadata files in sync with environment settings.
- The runner watches plugin DLL roots (see `WorkspaceWatchers` in [DataverseDebugger.Runner/RunnerPipeServer.cs](DataverseDebugger.Runner/RunnerPipeServer.cs)) and shadow-copies changes into `%AppData%/envcache/.../runner-shadow`. This enables hot reload without restarting the host.

## Debugging & Tooling
- Visual Studio automation is baked into the host, but VS Code users must follow [VSCODE_DEBUGGING.md](VSCODE_DEBUGGING.md) and attach manually to `DataverseDebugger.Runner.exe` using the CLR attach configuration.
- Runner log levels/categories can be toggled through the UI which ultimately calls `RunnerClient.UpdateRunnerLogConfigAsync`; use the same pipe messages in automation or tests when diagnosing.
- The embedded Dataverse REST Builder ships as `extensions/DataverseRESTBuilder_1_0_0_43_managed.zip` (preserved via `CopyToOutputDirectory` in [DataverseDebugger.App/DataverseDebugger.App.csproj](DataverseDebugger.App/DataverseDebugger.App.csproj)).

## Packaging & Versioning
- Version numbers flow from `ProductVersion` inside [Directory.Build.props](Directory.Build.props) and `Version.props`; Debug builds get an `-debug` informational suffix.
- `CopyRunnerOnPublish` ensures the freshly built `net48` runner is copied into `publish/runner`, and `ZipPublishOutput` archives the publish folder into `artifacts/DataverseDebugger-<ProductVersion>.zip` (see [DataverseDebugger.App/DataverseDebugger.App.csproj](DataverseDebugger.App/DataverseDebugger.App.csproj)).
- Keep the runner and host protocols version-aligned before packaging; mismatches surface as `ProtocolVersion` errors in pipe responses.

## Repository Layout Cues
- [DataverseDebugger.RestBuilder](DataverseDebugger.RestBuilder) is a static web payload consumed by the host; changes must keep `drb_index.htm` self-contained because it loads inside WebView2 without external bundlers.
- [artifacts](artifacts) is git-ignored and used only for publish zips; do not check binaries in here.
- PNG/ICO assets referenced by WPF live at the repo root (e.g., `dd_logo.png`) and are linked via `Resource` items in the app project.

Please let me know if any part of this guide is unclear or missing important workflows so we can refine it.
