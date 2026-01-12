<h1 style="margin:0;">
	<span style="display:inline-block; vertical-align:middle; height:1em;">
		<img src="dd_logo.png" alt="Dataverse Debugger logo" style="display:block; height:2em; width:auto;" />
	</span>
	<span style="display:inline-block; vertical-align:middle; margin-left:0.5rem;">Dataverse Debugger</span>
</h1>

Dataverse Debugger is a Windows desktop tool that lets you reproduce Microsoft Dataverse requests locally, attach Visual Studio, and validate changes before you ever redeploy to the tenant.

[![Build and Release](https://github.com/RamonvanHinthem/DataverseDebugger/actions/workflows/build-and-release.yml/badge.svg)](https://github.com/RamonvanHinthem/DataverseDebugger/actions/workflows/build-and-release.yml)

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4) ![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4) ![JavaScript](https://img.shields.io/badge/JavaScript-ES2020-F7DF1E) ![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)

## In a Nutshell

- **Capture real traffic** from a model-driven app rendered inside the debugger.
- **Replay the payload locally** so your plugin DLL runs inside the bundled runner process (with Fake or Real writes).
- **Attach Visual Studio** to the runner, set breakpoints, and inspect the full pipeline.
- **Switch execution targets** (Live vs. Debugger bridge) per request without changing code.
- **Compose ad-hoc calls** using the embedded Dataverse REST Builder and send them to either target.
- **Impersonate users and toggle form helpers** (God Mode, logical names, changed fields) to understand the exact context.

## Typical Flow

1. Launch `DataverseDebugger.App`, sign in, and register the plugin assemblies you want to debug.
2. Use the built-in browser to reproduce the scenario; every request is captured and listed in the Collection tree.
3. Pick a captured node, adjust configuration (entity, impersonation, headers, etc.), and choose **Send to Debugger** to execute it against your local runner.
4. Attach Visual Studio if you need step-through debugging; the runner automatically hot-reloads assemblies when you rebuild.
5. When the trace looks right, flip the target back to **Live** and replay the same payload against the Dataverse environment.

## When to Use It

- You need to debug synchronous plugins or Custom APIs without deploying to Dataverse.
- You want to inspect the full execution trace (timings, `ITracingService` output, request/response bodies).
- You are building FetchXML/CRUD calls and want to send them either to Dataverse or to your local debugger using the same UI.
- You must test impersonation scenarios or confirm how field-level changes impact the payload before saving.

## Execution Modes (Plugin Debugging)

The runner supports three execution modes for plugin debugging payloads:

- **Offline**: fully in-memory; no live Dataverse calls. `Execute` supports WhoAmI only.
- **Hybrid**: cached writes + live reads via a ServiceClient-backed `IOrganizationService`. Cached creates are only injected for ID-targeted queries.
- **Online**: live reads + writes via ServiceClient. Writes are gated by `DATAVERSE_DEBUGGER_ALLOW_LIVE_WRITES`.

Execution mode is driven by the `ExecutionMode` request field (`Offline`, `Hybrid`, `Online`). If absent, legacy `WriteMode` mapping applies.

## Known Limitations

- Plugin execution is single-step (no pipeline re-entry simulation).
- Offline supports WhoAmI only for `Execute`.
- Hybrid `Execute` is Whitelisted to WhoAmI only.
- Access tokens are supplied per request; token refresh is not handled by the runner.
- Host-side Web API proxy routing remains separate from the runner's plugin debugging pipeline.

## Requirements

- Windows 10/11 with the [WebView2 runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) installed (run the Evergreen bootstrapper once if needed).
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and the [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/download/dotnet-framework/net48).
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (optional but recommended for debugging).


## TODO
- Darkmode
- Webresource Debugging (fiddler style)
- Fake plugin registration
- Expanded offline debugging UX

## Related Docs

- [ARCHITECTURE.md](ARCHITECTURE.md) – how the processes communicate.


Licensed under MIT. See `THIRD_PARTY_NOTICES.txt` for bundled components. Use only in development/test environments.
