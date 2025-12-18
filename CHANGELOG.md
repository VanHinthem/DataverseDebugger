# Changelog

All notable changes to this project will be documented here.

## [1.0.0] - 2026-01-05
### Added
- Modern WPF host (`DataverseDebugger.App`) with embedded WebView2 to capture and replay Dataverse traffic inside the debugger UI.
- Isolated .NET Framework runner (`DataverseDebugger.Runner`) hosted via named pipes, enabling local plugin execution with hot reload and Visual Studio attach.
- Request routing pipeline that classifies Create/Update/Delete/Custom API/$batch calls for local emulation while proxying unsupported patterns for fidelity.
- Integrated Dataverse REST Builder experience packaged as `DataverseRESTBuilder.bundle.zip`, including request composer and debugger-target toggles.
- Environment-aware authentication, metadata caching, impersonation helpers, and execution trace viewer for full pipeline introspection.
- Publish pipeline that copies the runner payload, ships the REST Builder bundle, and archives the app output for distribution.
