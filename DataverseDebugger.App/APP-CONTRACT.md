# DataverseDebugger.App Contract (Execution Semantics)

The App is a thin host. The Runner is authoritative for execution semantics.

Allowed responsibilities:
- collect inputs and persist settings
- provide UX gating/confirmation for AllowLiveWrites
- start/stop/restart the Runner process
- pass ExecutionMode and AllowLiveWrites to the Runner
- display traces and results

Prohibited responsibilities:
- implement Offline/Hybrid/Online behavior in the App
- implement overlay/merge/whitelist logic
- decide request allowance beyond UX confirmation

Data flow:
- ExecutionMode is sent per request (protocol field)
- AllowLiveWrites is passed at Runner process start (env var)
- WriteMode is derived for legacy compatibility only
