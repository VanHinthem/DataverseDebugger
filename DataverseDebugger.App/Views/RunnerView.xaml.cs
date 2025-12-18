using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Runner;
using DataverseDebugger.App.Services;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View for monitoring and controlling the Runner process.
    /// </summary>
    /// <remarks>
    /// Displays runner health status, trace output, and provides controls
    /// for starting, stopping, and restarting the runner.
    /// </remarks>
    public partial class RunnerView : UserControl
    {
        private readonly RunnerClient _runnerClient;
        private readonly RunnerHealthViewModel _vm;
        public ObservableCollection<string> GlobalRunnerTrace { get; }
        public RunnerHealthViewModel Vm => _vm;
        private ObservableCollection<string>? _traceLines;
        private ObservableCollection<string>? _globalTraceLines;
        public RunnerView(RunnerClient runnerClient, RunnerHealthViewModel vm, ObservableCollection<string> globalRunnerTrace)
        {
            InitializeComponent();
            _runnerClient = runnerClient;
            _vm = vm;
            GlobalRunnerTrace = globalRunnerTrace;
            DataContext = this;
            RunnerTraceGrid.IsVisibleChanged += OnRunnerTraceVisibilityChanged;
            GlobalRunnerTraceGrid.IsVisibleChanged += OnGlobalTraceVisibilityChanged;
            AttachTraceLines(_vm.TraceLines);
            AttachGlobalTraceLines(GlobalRunnerTrace);
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        public async Task ApplyEnvironmentAsync(EnvironmentProfile profile)
        {
            var metadataPath = MetadataCacheService.GetMetadataPath(profile);
            var shadowRoot = EnvironmentPathService.GetRunnerShadowRoot(profile);
            var entityMetadataCacheRoot = EnvironmentPathService.GetEnvironmentCacheRoot(profile);
            Directory.CreateDirectory(entityMetadataCacheRoot);
            _vm.InitStatusText = "Sending init workspace...";
            _vm.ExecuteStatusText = string.Empty;
            var request = new InitializeWorkspaceRequest
            {
                Environment = new EnvConfig
                {
                    Name = profile.Name,
                    OrgUrl = profile.OrgUrl,
                    MetadataPath = metadataPath,
                    RunnerShadowRoot = shadowRoot,
                    EntityMetadataCacheRoot = entityMetadataCacheRoot,
                    EmulationEnabled = true
                },
                Workspace = new PluginWorkspaceManifest
                {
                    DisableAsyncStepsOnServer = false,
                    TraceVerbose = profile.TraceVerbose,
                    Assemblies = BuildAssemblyList(profile.PluginAssemblies)
                }
            };

            var response = await _runnerClient.InitializeWorkspaceAsync(request, TimeSpan.FromSeconds(10));
            _vm.InitStatusText = response.Message ?? $"Init status: {response.Status}";
        }

        private static System.Collections.Generic.List<PluginAssemblyRef> BuildAssemblyList(System.Collections.Generic.IEnumerable<string> assemblyPaths)
        {
            var list = new System.Collections.Generic.List<PluginAssemblyRef>();
            foreach (var path in assemblyPaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }
                list.Add(new PluginAssemblyRef { Path = path, Enabled = true });
            }
            return list;
        }

        public void ApplyCatalog(PluginCatalog catalog, System.Collections.Generic.IEnumerable<string>? selectedAssemblyPaths = null)
        {
            if (catalog == null) return;

            var selectedNames = new System.Collections.Generic.HashSet<string>(
                selectedAssemblyPaths?
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Select(p => Path.GetFileNameWithoutExtension(p) ?? string.Empty)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            var assemblies = catalog.Assemblies;
            var types = catalog.Types;
            var steps = catalog.Steps;
            var images = catalog.Images;

            if (selectedNames.Count > 0)
            {
                assemblies = assemblies
                    .Where(a => selectedNames.Contains(Path.GetFileNameWithoutExtension(a.Name) ?? string.Empty) ||
                                selectedNames.Contains(a.Name))
                    .ToList();

                var allowedAssemblyIds = new System.Collections.Generic.HashSet<Guid>(assemblies.Select(a => a.Id));
                types = types
                    .Where(t =>
                        (t.AssemblyId != Guid.Empty && allowedAssemblyIds.Contains(t.AssemblyId)) ||
                        selectedNames.Contains(Path.GetFileNameWithoutExtension(t.AssemblyName) ?? string.Empty) ||
                        selectedNames.Contains(t.AssemblyName))
                    .ToList();

                var allowedTypeIds = new System.Collections.Generic.HashSet<Guid>(types.Select(t => t.Id));
                steps = steps
                    .Where(s => allowedTypeIds.Contains(s.PluginTypeId))
                    .ToList();

                var allowedStepIds = new System.Collections.Generic.HashSet<Guid>(steps.Select(s => s.Id));
                images = images
                    .Where(img => allowedStepIds.Contains(img.StepId))
                    .ToList();
            }

            LogService.Append($"Catalog loaded: {assemblies.Count} assemblies, {types.Count} types, {steps.Count} steps, {images.Count} images.");

            // Build lookup tables once for O(1) access
            var typesByAssemblyId = types.ToLookup(t => t.AssemblyId);
            var stepsByTypeId = steps.ToLookup(s => s.PluginTypeId);
            var imagesByStepId = images.ToLookup(i => i.StepId);
            var typesById = types.ToDictionary(t => t.Id);

            foreach (var asm in assemblies)
            {
                LogService.Append($"Assembly: {asm.Name} ({asm.Id}) Version={asm.Version}, PKT={asm.PublicKeyToken}, Isolation={asm.IsolationMode}, Managed={asm.IsManaged}");
            }

            foreach (var type in types)
            {
                var typeSteps = stepsByTypeId[type.Id];
                var typeStepIds = typeSteps.Select(s => s.Id);
                var typeImages = typeStepIds.Sum(stepId => imagesByStepId[stepId].Count());
                LogService.Append($"Type: {type.TypeName} (Asm={type.AssemblyName}, Id={type.Id}) Steps={typeSteps.Count()}, Images={typeImages}");
            }

            //foreach (var step in steps)
            //{
            //    typesById.TryGetValue(step.PluginTypeId, out var type);
            //    var imgCount = imagesByStepId[step.Id].Count();
            //    var filter = step.FilteringAttributes ?? string.Empty;
            //    LogService.Append($"Step: {step.Message} / {step.PrimaryEntity} (Stage {step.Stage}, Mode {step.Mode}, Rank {step.Rank}, Filter {filter}), Type={type?.TypeName}, Images={imgCount}");

            //    foreach (var img in imagesByStepId[step.Id])
            //    {
            //        LogService.Append($"  Image: {img.ImageType} Alias={img.EntityAlias} Attrs={img.Attributes}");
            //    }
            //}
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(RunnerHealthViewModel.TraceLines), StringComparison.Ordinal))
            {
                AttachTraceLines(_vm.TraceLines);
            }
        }

        private void AttachTraceLines(ObservableCollection<string>? traceLines)
        {
            if (_traceLines != null)
            {
                _traceLines.CollectionChanged -= OnTraceLinesChanged;
            }

            _traceLines = traceLines;
            if (_traceLines != null)
            {
                _traceLines.CollectionChanged += OnTraceLinesChanged;
            }

            ScrollToLatest(RunnerTraceGrid, _traceLines, forceToEnd: true);
        }

        private void AttachGlobalTraceLines(ObservableCollection<string>? traceLines)
        {
            if (_globalTraceLines != null)
            {
                _globalTraceLines.CollectionChanged -= OnGlobalTraceLinesChanged;
            }

            _globalTraceLines = traceLines;
            if (_globalTraceLines != null)
            {
                _globalTraceLines.CollectionChanged += OnGlobalTraceLinesChanged;
            }

            ScrollToLatest(GlobalRunnerTraceGrid, _globalTraceLines, forceToEnd: true);
        }

        private void OnTraceLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScrollToLatest(RunnerTraceGrid, _traceLines);
        }

        private void OnGlobalTraceLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScrollToLatest(GlobalRunnerTraceGrid, _globalTraceLines);
        }

        private void ScrollToLatest(DataGrid? grid, ObservableCollection<string>? lines, bool forceToEnd = false)
        {
            if (grid == null || lines == null || lines.Count == 0)
            {
                return;
            }

            if (!forceToEnd && !grid.IsVisible)
            {
                return;
            }

            void Apply()
            {
                var viewer = GetScrollViewer(grid);
                var shouldScroll = forceToEnd;
                if (!shouldScroll && viewer != null)
                {
                    var distanceFromBottom = viewer.ExtentHeight - viewer.ViewportHeight - viewer.VerticalOffset;
                    shouldScroll = distanceFromBottom <= 1.0;
                }

                if (!shouldScroll)
                {
                    return;
                }

                var last = lines[lines.Count - 1];
                grid.ScrollIntoView(last);
            }

            var dispatcher = grid.Dispatcher ?? Dispatcher;
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Apply));
        }

        private void OnRunnerTraceVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (RunnerTraceGrid.IsVisible)
            {
                ScrollToLatest(RunnerTraceGrid, _traceLines, forceToEnd: true);
            }
        }

        private void OnGlobalTraceVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (GlobalRunnerTraceGrid.IsVisible)
            {
                ScrollToLatest(GlobalRunnerTraceGrid, _globalTraceLines, forceToEnd: true);
            }
        }

        private static ScrollViewer? GetScrollViewer(DependencyObject? start)
        {
            if (start == null)
            {
                return null;
            }

            if (start is ScrollViewer viewer)
            {
                return viewer;
            }

            var count = VisualTreeHelper.GetChildrenCount(start);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(start, i);
                var result = GetScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
