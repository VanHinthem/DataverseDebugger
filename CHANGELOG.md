# Changelog

All notable changes to this project will be documented here.

## [1.1.1] - 2026-02-09
### Fixed
- WebView network override settings and improve DevTools integration


## [1.1.0] - 2026-01-16
### Added
- Offline, Hybrid, and Online execution modes in Runner and App
- Plugin overview tab in App
- WebResource AutoResponder rules (Exact/Wildcard/Regex; ServeLocalFile/ServeFromFolder/ProxyToUrl)
- Inline source maps when serving local WebResources
### Fixed
- Minor fixes

## [1.0.2] - 2026-01-07
### Added
- Implemented Rest Builder caching
- Added Dark Mode


## [1.0.1] - 2026-01-06
### Added
- Implemented ILogger, IServiceEndpointNotificationService and IFeatureControlService into the plugin execution context
### Fixed
- Aligned Capture, Auto Proxy and Debug toggles in Browser, REST Builder and Requests
- Version handling


## [1.0.0] - 2026-01-05
### Added
- Modern WPF host (`DataverseDebugger.App`) with embedded WebView2 to capture and replay Dataverse traffic inside the debugger UI.
- Isolated .NET Framework runner (`DataverseDebugger.Runner`) hosted via named pipes, enabling local plugin execution with hot reload and Visual Studio attach.
- Request routing pipeline that classifies Create/Update/Delete/Custom API/$batch calls for local emulation while proxying unsupported patterns for fidelity.
- Integrated Dataverse REST Builder experience packaged as `DataverseRESTBuilder.bundle.zip`, including request composer and debugger-target toggles.
- Environment-aware authentication, metadata caching, impersonation helpers, and execution trace viewer for full pipeline introspection.
- Publish pipeline that copies the runner payload, ships the REST Builder bundle, and archives the app output for distribution.
