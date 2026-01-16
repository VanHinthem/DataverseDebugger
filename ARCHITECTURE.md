# Dataverse Debugger - Architecture

Modern WPF host (.NET 8) paired with an isolated .NET Framework 4.8 runner that executes Microsoft Dataverse plugins locally, supports true unload/hot reload, and enables step-through debugging.

## Implementation Status

✅ **Implemented:**
- Two-process architecture (WPF host + net48 runner)
- Named Pipes IPC with JSON protocol
- WebView2 browser integration with request interception
- Request capture, proxy, and local execution
- Plugin pipeline execution (PreValidation → PreOperation → PostOperation)
- Visual Studio debugger attachment via COM automation
- Hot reload via runner restart on DLL changes
- Per-environment configuration and caching
- MSAL authentication with per-environment token cache
- OData metadata-driven request parsing
- Embedded DataverseRESTBuilder
- WebResource AutoResponder overrides (Exact/Wildcard/Regex; local file/folder or proxy)

🔄 **In Progress:**
- Session persistence (save/reload captures)
- Enhanced step metadata display

📋 **Planned:**
- Async step improvements

## Goals
- Load and debug multiple Dataverse plugin assemblies (net462/net48) locally.
- Preserve Model-Driven App fidelity via correct routing (proxy vs emulate).
- Achieve real unload/hot reload by isolating plugin execution in a separate process.
- Reuse the embedded browser's bearer token for Dataverse calls.
- Support host-side web resource overrides/debugging.

## Non-goals (initial)
- No in-proc plugin unloading on .NET Framework.
- No emulation of Retrieve/RetrieveMultiple (kept proxied for fidelity).

## High-level architecture
- **Process 1: WPF Host** (`DataverseDebugger.App`, `net8.0-windows`)
  - Embeds WebView2 for Model-Driven Apps
  - Intercepts HTTP requests via DevTools protocol, routes Proxy vs Emulate
  - Manages runner lifecycle (start/stop/restart), hot reload, health monitoring
  - UI for environments, browser, requests, traces, execution trees, settings
  - Visual Studio debugger attachment via COM automation
  - Embedded DataverseRESTBuilder for crafting Web API requests
  - Per-environment metadata and plugin catalog caching
- **Process 2: Plugin Runner** (`DataverseDebugger.Runner`, `net48`)
  - Loads plugin assemblies via shadow copy (no file locks)
  - Discovers steps from cached plugin catalog
  - Executes pipeline locally with stub execution context
  - Uses host-forwarded bearer token to call Dataverse Web API
  - Returns HTTP response + execution trace (steps, timings, trace lines)

## IPC
- **Transport:** Named Pipes on Windows with length-prefixed JSON messages
- **Protocol:** Shared DTOs in `DataverseDebugger.Protocol` (.NET Standard 2.0) with `ProtocolVersion` and capability flags
- **Streaming:** Trace events streamed during execution for live UI updates

### Implemented RPCs
- `InitializeWorkspace` - Loads assemblies, configures environment, returns capabilities
- `Execute` - Executes Web API request through pipeline, returns response + trace
- `ExecutePlugin` - Executes a specific plugin step with provided context
- `HealthCheck` - Verifies runner is alive and responsive
- `GetRunnerLogs` - Retrieves runner-side log entries
- `ConfigureLogging` - Adjusts runner log level and categories

## Request routing (host)
- **Proxy always:** Retrieve, RetrieveMultiple, unknown/unsupported patterns.
- **Emulate locally:** Create, Update, Delete, Custom Actions/APIs, `$batch` (with changesets and continue-on-error).
- `$batch`: parse into subrequests; preserve transactional semantics; fallback to proxy if parsing fails.

## Request parsing (host)
- OData metadata-driven parsing maps method + URL to message and entity set for step matching.
- Supported message mapping: Create, Update, Delete, Retrieve, RetrieveMultiple, Custom Actions/APIs, `$batch` -> ExecuteMultiple.
- If metadata is missing or parsing fails, fall back to heuristic matching (method + URL).

## Host responsibilities (WPF)
- WebView2 integration, `WebResourceRequested` interception, response injection.
- Routing decisions per request.
- Request history UI with execution tree, traces, timings.
- Runner lifecycle: start/stop, health checks, auto-restart on crash; hot reload via file watch + restart.
- Per-environment config/workspace:
  - `AssemblyPaths[]` (multiple DLLs) + optional `SymbolPaths[]`.
  - Dataverse org info (URL/host, friendly name) and Navigate URL.
  - Flags: trace verbosity, disable async steps, capture defaults (API-only, auto-proxy).
  - Per-environment token cache and WebView cache folders.
- Authentication: interactive login per environment (MSAL cache) for metadata/catalog fetch; captured request tokens forwarded to runner for proxying.
- Web resource overrides:
  - AutoResponder rules for WebResources (Exact/Wildcard/Regex; ServeLocalFile/ServeFromFolder/ProxyToUrl).
  - Intercept resource requests; serve local content with correct `Content-Type`.
  - Inline source maps when serving local JS for DevTools debugging.
- Debug attach: host lets user pick a running Visual Studio instance and attach to the runner process.
- Overlay status: runner reload uses the same loading overlay as environment activation; WebView2 is hidden while overlay is visible.

## Runner responsibilities (net48)
- Workspace load:
  - Load assemblies from bytes (no file locks); resolve dependencies from assembly directories or configured lib paths.
  - Discover steps: pluginassembly, plugintype, sdkmessageprocessingstep, images, filtering attributes, stage/mode/rank.
- Pipeline execution:
  - PreValidation -> PreOperation -> PostOperation; sync + async (async emulated immediately unless disabled).
  - Preserve filtering attributes, images, depth, parent correlation.
  - Provide `ITracingService`, `IOrganizationServiceFactory`; track nested calls for execution tree.
- Dataverse calls:
  - Debugging execution uses ServiceClient-backed `IOrganizationService` for live connectivity.
  - Host-side Web API proxy routing remains separate from the runner pipeline.
  - SDK-style requests optional; default to Web API for proxy fidelity.
- Async step handling:
  - Optional "disable async steps on server" per assembly; best-effort re-enable on shutdown/startup.
- Telemetry to host:
  - Final HTTP response (status/headers/body).
  - Execution trace tree (steps, timings, traces, exceptions).
  - Live trace streaming if transport supports it.

## Hot reload/unload
- Host watches all plugin DLL paths (debounced).
- On change: mark runner draining, wait for in-flight requests (or timeout), kill runner, start new runner, send workspace manifest.
- Runner process exit provides true unload.

## Multi-assembly support
- Workspace manifest includes multiple assemblies; runner associates steps by `plugintype.pluginassemblyid`.
- UI may show per-assembly enable/disable and step attribution.

## Web resource debugging (future)
- Host-side only:
  - Intercept web resource URLs; serve local files when mapped.
  - Support source maps; optional SCSS/LESS build later.
  - Toggles per environment: enable overrides, auto-reload on save.
- Runner remains focused on .NET plugin execution.

## Authentication
- Interactive login per environment:
  - MSAL token cache stored per environment; used for metadata and catalog fetch.
  - WebView2 sign-in remains separate; requests still include bearer tokens for proxying.
- Runner uses the forwarded request headers for HTTP proxy calls; no separate login required for proxied traffic.

## Execution modes (plugin debugging)
- **Offline**: in-memory execution only; no live calls. `Execute` supports WhoAmI only.
- **Hybrid**: cached writes + live reads using ServiceClient. Cache overlays live results; cached creates appear only for ID-targeted queries. `Execute` is Whitelisted to WhoAmI only.
- **Online**: live reads + writes using ServiceClient. Writes are gated by `DATAVERSE_DEBUGGER_ALLOW_LIVE_WRITES`.
- `ExecutionMode` is authoritative when provided; legacy `WriteMode` fallback applies when absent.
- Token refresh is not implemented in the runner; a fresh token is expected per request.

## Failure handling
- Runner crash -> host auto-restart; Proxy-only until ready.
- Parse/unsupported request -> proxy fallback.
- Async disablement recovery on runner shutdown/startup.

## Build & packaging
- Host: `net8.0-windows` (or `net9.0-windows`), WPF, WebView2 runtime required.
- Runner: `net48` (or `net462` if mandated); console/service-style.
- Shared protocol: `netstandard2.0`.
- Distribution: installer or zip; ensure WebView2 runtime and .NET prerequisites.

## First vertical slice (recommended)
1) Host: WPF shell with WebView2, request logging, Proxy-only.  
2) IPC: Named Pipe server in runner; host can send a ping and get a reply.  
3) Emulation minimal: Create with single plugin assembly; return trace.  
4) Add Update/Delete and custom action dispatch.  
5) Add multi-assembly workspace and restart-on-change hot reload.  
6) Add `$batch` parsing with changesets; proxy fallback on parse failure.  
7) Add UI for execution tree + traces; debugger-attach helper to runner.  
8) Add web resource override path (host-only) + source map support.  

## Testing strategy
- Unit tests: routing (Proxy vs Emulate), `$batch` parsing, request -> message classification.
- Golden tests: execution tree structure and trace content (runner-side).
- Integration: host + runner loopback with sample plugin assembly.
- Health check watchdog: runner liveness + IPC ping.


