using System.ComponentModel;
using System.Runtime.CompilerServices;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Settings for runner process logging configuration.
    /// </summary>
    /// <remarks>
    /// Controls log level and which categories of log messages are enabled.
    /// </remarks>
    public sealed class RunnerLogSettingsModel : INotifyPropertyChanged
    {
        private RunnerLogLevel _level = RunnerLogLevel.Info;
        private bool _runnerLifecycle = true;
        private bool _ipc = true;
        private bool _workspaceInit = true;
        private bool _assemblyLoad = true;
        private bool _pluginCache = true;
        private bool _metadata = true;
        private bool _emulator = true;
        private bool _debugger = true;
        private bool _perf = true;
        private bool _errors = true;

        /// <summary>Gets or sets the minimum log level.</summary>
        public RunnerLogLevel Level
        {
            get => _level;
            set => SetField(ref _level, value);
        }

        public bool RunnerLifecycle
        {
            get => _runnerLifecycle;
            set => SetField(ref _runnerLifecycle, value);
        }

        public bool Ipc
        {
            get => _ipc;
            set => SetField(ref _ipc, value);
        }

        public bool WorkspaceInit
        {
            get => _workspaceInit;
            set => SetField(ref _workspaceInit, value);
        }

        public bool AssemblyLoad
        {
            get => _assemblyLoad;
            set => SetField(ref _assemblyLoad, value);
        }

        public bool PluginCache
        {
            get => _pluginCache;
            set => SetField(ref _pluginCache, value);
        }

        public bool Metadata
        {
            get => _metadata;
            set => SetField(ref _metadata, value);
        }

        public bool Emulator
        {
            get => _emulator;
            set => SetField(ref _emulator, value);
        }

        public bool Debugger
        {
            get => _debugger;
            set => SetField(ref _debugger, value);
        }

        public bool Perf
        {
            get => _perf;
            set => SetField(ref _perf, value);
        }

        public bool Errors
        {
            get => _errors;
            set => SetField(ref _errors, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RunnerLogCategory ToCategories()
        {
            var categories = RunnerLogCategory.None;
            if (RunnerLifecycle) categories |= RunnerLogCategory.RunnerLifecycle;
            if (Ipc) categories |= RunnerLogCategory.Ipc;
            if (WorkspaceInit) categories |= RunnerLogCategory.WorkspaceInit;
            if (AssemblyLoad) categories |= RunnerLogCategory.AssemblyLoad;
            if (PluginCache) categories |= RunnerLogCategory.PluginCache;
            if (Metadata) categories |= RunnerLogCategory.Metadata;
            if (Emulator) categories |= RunnerLogCategory.Emulator;
            if (Debugger) categories |= RunnerLogCategory.Debugger;
            if (Perf) categories |= RunnerLogCategory.Perf;
            if (Errors) categories |= RunnerLogCategory.Errors;
            return categories;
        }

        public void ApplyCategories(RunnerLogCategory categories)
        {
            RunnerLifecycle = categories.HasFlag(RunnerLogCategory.RunnerLifecycle);
            Ipc = categories.HasFlag(RunnerLogCategory.Ipc);
            WorkspaceInit = categories.HasFlag(RunnerLogCategory.WorkspaceInit);
            AssemblyLoad = categories.HasFlag(RunnerLogCategory.AssemblyLoad);
            PluginCache = categories.HasFlag(RunnerLogCategory.PluginCache);
            Metadata = categories.HasFlag(RunnerLogCategory.Metadata);
            Emulator = categories.HasFlag(RunnerLogCategory.Emulator);
            Debugger = categories.HasFlag(RunnerLogCategory.Debugger);
            Perf = categories.HasFlag(RunnerLogCategory.Perf);
            Errors = categories.HasFlag(RunnerLogCategory.Errors);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
