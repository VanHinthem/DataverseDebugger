# VS Code Debugging (Runner + Plugins)

This project can be debugged with VS Code by manually attaching to the runner process.
The built-in Debug toggle in the app only supports Visual Studio automation and will not reflect VS Code attach status.

## Prerequisites
- VS Code
- C# extension (ms-dotnettools.csharp)
- Debug build of the runner and your plugin assemblies (PDBs must be produced)

## Attach to the Runner
1) Start the Dataverse Debugger app and activate an environment so the runner starts.
2) In VS Code, create `.vscode/launch.json` with:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Attach DataverseDebugger.Runner",
      "type": "clr",
      "request": "attach",
      "processId": "${command:pickProcess}",
      "justMyCode": false
    }
  ]
}
```

3) Run the config and pick `DataverseDebugger.Runner.exe` from the process list.

## Breakpoints in Plugins
- Build your plugin project in **Debug** so PDBs are present next to the DLL.
- The runner shadow-copies plugin DLLs (and PDBs) into:
  `DataverseDebugger.App/bin/Debug/net8.0-windows/envcache/<envNumber>/runner-shadow/<pid>/...`
- If breakpoints do not bind, open the runner log and look for:
  `Shadow copy created: <original> -> <shadow>`
  Then make sure the PDB exists in the shadow folder.

## Notes
- Slow Dataverse calls can block health checks (runner is single-connection).
- VS Code attach is manual; the app won’t show “Debugging active.”
