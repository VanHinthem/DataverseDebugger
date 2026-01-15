using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using DataverseDebugger.Protocol;
using DataverseDebugger.App.Runner;
using DataverseDebugger.App.Views;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Auth;
using DataverseDebugger.App.Services;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Reflection;

namespace DataverseDebugger.App
{
    /// <summary>
    /// Main application window hosting navigation and all views.
    /// </summary>
    /// <remarks>
    /// Coordinates the Runner process lifecycle, environment activation,
    /// health monitoring, and section navigation.
    /// </remarks>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty IsSidebarCollapsedProperty = DependencyProperty.Register(
            nameof(IsSidebarCollapsed),
            typeof(bool),
            typeof(MainWindow),
            new PropertyMetadata(false, OnIsSidebarCollapsedChanged));

        private readonly RunnerClient _runnerClient = new RunnerClient();
        private readonly RunnerProcessManager _runnerProcess = new RunnerProcessManager();
        private readonly DispatcherTimer _healthTimer = new DispatcherTimer();
        private bool _healthCheckInProgress;
        private readonly RunnerHealthViewModel _runnerVm = new RunnerHealthViewModel();
        private readonly RunnerView _runnerView;
        private readonly BrowserView _browserView;
        private readonly RestBuilderView _restBuilderView;
        private readonly RequestsView _requestsView;
        private readonly PluginsView _pluginsView;
        private readonly EnvironmentsView _environmentsView;
        private readonly SettingsView _settingsView;
        private readonly ObservableCollection<CapturedRequest> _requests = new();
        private readonly ObservableCollection<string> _globalTrace = new();
        private readonly CaptureSettingsModel _captureSettings = new CaptureSettingsModel();
        private readonly BrowserSettingsModel _browserSettings;
        private readonly RunnerLogSettingsModel _runnerLogSettings;
        private readonly RunnerSettingsModel _runnerSettings;
        private readonly AppearanceSettingsModel _appearanceSettings;
        private readonly string _defaultExecutionMode;
        private readonly bool _defaultAllowLiveWrites;
        private readonly DispatcherTimer _runnerLogTimer = new DispatcherTimer();
        private long _runnerLogLastId;
        private bool _runnerLogPollInProgress;
        private bool _runnerLogConfigInProgress;
        private EnvironmentProfile? _activeProfile;
        private PluginCatalog? _activeCatalog;
        private bool _environmentReady;
        private bool _isEnvironmentLoading;
        private bool _runnerReloading;
        private bool _pluginDebuggingActive;
        private bool _webViewHiddenForOverlay;
        private EnvironmentProfile? _restBuilderAppliedProfile;
        private Task? _restBuilderInitTask;
        private DateTime? _runnerReloadShownAtUtc;
        private readonly TimeSpan _runnerReloadMinVisible = TimeSpan.FromMilliseconds(800);
        private DispatcherTimer? _runnerReloadHideTimer;
        private bool _suppressRunnerSettingsHandling;
        private string _lastExecutionMode = "Hybrid";
        private bool _lastAllowLiveWrites;
        private bool _closeAfterCleanup;
        private bool _closeCleanupInProgress;
        private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RestBuilderInjectionTimeout = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan RestBuilderInjectionRetryInterval = TimeSpan.FromMilliseconds(200);

        private const string BaseWindowTitle = "Dataverse Debugger";

        public bool IsSidebarCollapsed
        {
            get => (bool)GetValue(IsSidebarCollapsedProperty);
            set => SetValue(IsSidebarCollapsedProperty, value);
        }

        public MainWindow()
        {
            InitializeComponent();
            VersionTextBlock.Text = $"v{GetAppVersion()}";
            EnvironmentPathService.CleanupAllRunnerShadowRoots();
            DataContext = _runnerVm;
            var appSettings = AppSettingsService.Load();
            _browserSettings = appSettings.Browser;
            _runnerLogSettings = appSettings.RunnerLog;
            _runnerSettings = appSettings.Runner;
            _appearanceSettings = appSettings.Appearance;
            _defaultExecutionMode = _runnerSettings.ExecutionMode;
            _defaultAllowLiveWrites = _runnerSettings.AllowLiveWrites;
            
            // Initialize theme before creating views
            ThemeService.Initialize(_appearanceSettings.IsDarkMode);
            _appearanceSettings.PropertyChanged += OnAppearanceSettingsChanged;
            
            _settingsView = new SettingsView(_browserSettings, _runnerLogSettings, _runnerSettings, _appearanceSettings);
            _browserSettings.PropertyChanged += OnBrowserSettingsChanged;
            _runnerLogSettings.PropertyChanged += OnRunnerLogSettingsChanged;
            _runnerSettings.PropertyChanged += OnRunnerSettingsChanged;
            _lastExecutionMode = _runnerSettings.ExecutionMode;
            _lastAllowLiveWrites = _runnerSettings.AllowLiveWrites;
            _runnerLogTimer.Interval = TimeSpan.FromSeconds(1);
            _runnerLogTimer.Tick += async (_, _) => await FetchRunnerLogsAsync();
            _runnerView = new RunnerView(_runnerClient, _runnerVm, _globalTrace);
            _browserView = new BrowserView(_runnerClient, _requests, _globalTrace, _captureSettings, _browserSettings);
            _requestsView = new RequestsView(_requests, _runnerClient, _captureSettings, _runnerSettings, _globalTrace);
            _pluginsView = new PluginsView();
            _browserView.BeforeAutoProxyAsync = _requestsView.HandleAutoDebugBeforeProxyAsync;
            _browserView.AfterAutoProxyAsync = _requestsView.HandleAutoDebugAfterResponseAsync;
            _restBuilderView = new RestBuilderView(_runnerClient, _requests, _globalTrace, _captureSettings, _browserSettings);
            _restBuilderView.BeforeAutoProxyAsync = _requestsView.HandleAutoDebugBeforeProxyAsync;
            _restBuilderView.AfterAutoProxyAsync = _requestsView.HandleAutoDebugAfterResponseAsync;
            _restBuilderView.DebugToggleRequested = enabled => _browserView.RequestDebugToggle(enabled);
            _requestsView.DebugToggleRequested = enabled => _browserView.RequestDebugToggle(enabled);
            _requestsView.PluginDebuggingChanged += OnPluginDebuggingChanged;
            _requestsView.SendToBuilderRequested += OnSendRequestToBuilderAsync;
            _environmentsView = new EnvironmentsView(OnEnvironmentActivatedAsync, OpenBrowserSection, OnEnvironmentDeactivatedAsync);
            _runnerProcess.RunnerExited += OnRunnerExited;
            _environmentsView.ClearActiveProfiles(); // start with no active environment
            _activeProfile = null;
            _environmentReady = false;
            _isEnvironmentLoading = false;
            UpdateActiveEnvironmentUI();
            UpdateNavEnabled();
            SetNavSelection(NavEnvironments);
            ShowSection("Environments", NavEnvironments);
            UpdateSidebarVisualState();
        }

        private static string GetAppVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var infoVersion = SanitizeVersion(assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                return infoVersion;
            }

            var version = SanitizeVersion(assembly.GetName().Version?.ToString());
            return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
        }

        private static string? SanitizeVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            var plusIndex = version.IndexOf('+');
            if (plusIndex >= 0)
            {
                version = version.Substring(0, plusIndex);
            }

            return version?.Trim();
        }

        private static void OnIsSidebarCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow window)
            {
                window.UpdateSidebarVisualState();
            }
        }

        private void UpdateSidebarVisualState()
        {
            if (SidebarColumn != null)
            {
                SidebarColumn.Width = new GridLength(IsSidebarCollapsed ? 72 : 220);
            }

            if (SidebarToggleButton != null)
            {
                SidebarToggleButton.ToolTip = IsSidebarCollapsed ? "Expand menu" : "Collapse menu";
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _healthTimer.Interval = HealthCheckInterval;
            _healthTimer.Tick += async (_, _) => await RefreshHealthAsync();
            if (_activeProfile == null)
            {
                SetRunnerStoppedState();
                return;
            }

            if (!await EnsureRunnerAsync())
            {
                return;
            }

            StartHealthTimer();
            await RefreshHealthAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await RefreshHealthAsync();
        }

        private void OnToggleSidebarClick(object sender, RoutedEventArgs e)
        {
            IsSidebarCollapsed = !IsSidebarCollapsed;
        }

        private void OnPluginDebuggingChanged(bool active)
        {
            _pluginDebuggingActive = active;
        }

        private async Task OnSendRequestToBuilderAsync(RestBuilderInjectionRequest request)
        {
            if (request == null)
            {
                return;
            }

            try
            {
                ShowSection("REST Builder", NavRestBuilder);
                var injected = await InjectIntoRestBuilderWhenReadyAsync(request);
                if (!injected)
                {
                    MessageBox.Show("REST Builder is still loading. Please try again in a moment.", "REST Builder", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send request to REST Builder: {ex.Message}", "REST Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshHealthAsync()
        {
            if (_activeProfile == null)
            {
                SetRunnerStoppedState();
                return;
            }

            if (_isEnvironmentLoading || !_environmentReady)
            {
                return;
            }

            if (_browserView.IsDebuggingActive)
            {
                _runnerVm.StatusText = "Debugging active (health checks paused)";
                _runnerVm.InitStatusText = string.Empty;
                _runnerVm.ExecuteStatusText = string.Empty;
                return;
            }

            if (_pluginDebuggingActive)
            {
                _runnerVm.StatusText = "Plugin debugging active (health checks paused)";
                _runnerVm.InitStatusText = string.Empty;
                _runnerVm.ExecuteStatusText = string.Empty;
                return;
            }

            if (_runnerClient.HasActiveRequests)
            {
                _runnerVm.StatusText = "Requests in flight (health checks paused)";
                _runnerVm.InitStatusText = string.Empty;
                _runnerVm.ExecuteStatusText = string.Empty;
                return;
            }

            if (_healthCheckInProgress)
            {
                return;
            }

            _healthCheckInProgress = true;
            _runnerVm.StatusText = "Checking runner...";
            _runnerVm.InitStatusText = string.Empty;
            _runnerVm.ExecuteStatusText = string.Empty;
            SetBadges(HealthStatus.Unknown);

            try
            {
                var health = await _runnerClient.CheckHealthAsync(timeout: HealthCheckTimeout);
                _runnerVm.StatusText = health.Message ?? $"Status: {health.Status}";
                _runnerVm.CapabilitiesText = FormatCapabilities(health.Capabilities);
                SetBadges(health.Status);
                if (health.Status == HealthStatus.Ready && _runnerReloading)
                {
                    HideRunnerReloadToast();
                }
            }
            catch (Exception ex)
            {
                _runnerVm.StatusText = $"Runner health check failed: {ex.Message}";
                _runnerVm.CapabilitiesText = string.Empty;
                SetBadges(HealthStatus.Error);
            }
            finally
            {
                _healthCheckInProgress = false;
            }
        }

        private void SetBadges(HealthStatus status)
        {
            if (StoppedBadge != null)
            {
                StoppedBadge.Visibility = Visibility.Collapsed;
            }
            HealthyBadge.Visibility = status == HealthStatus.Ready ? Visibility.Visible : Visibility.Collapsed;
            DegradedBadge.Visibility = status == HealthStatus.Degraded ? Visibility.Visible : Visibility.Collapsed;
            ErrorBadge.Visibility = (status == HealthStatus.Error || status == HealthStatus.Unknown) ? Visibility.Visible : Visibility.Collapsed;
            _runnerVm.Status = status;
        }

        private void SetRunnerStoppedState()
        {
            _runnerProcess.Stop();
            if (_healthTimer.IsEnabled)
            {
                _healthTimer.Stop();
            }
            HideRunnerReloadToast();
            StopRunnerLogPolling();
            ResetRunnerLogState();

            _runnerVm.StatusText = "Runner stopped (no environment active)";
            _runnerVm.InitStatusText = string.Empty;
            _runnerVm.ExecuteStatusText = string.Empty;
            _runnerVm.CapabilitiesText = string.Empty;

            if (StoppedBadge != null)
            {
                StoppedBadge.Visibility = Visibility.Visible;
            }
            HealthyBadge.Visibility = Visibility.Collapsed;
            DegradedBadge.Visibility = Visibility.Collapsed;
            ErrorBadge.Visibility = Visibility.Collapsed;
            _runnerVm.Status = HealthStatus.Unknown;
        }

        private void StartHealthTimer()
        {
            if (!_healthTimer.IsEnabled)
            {
                _healthTimer.Start();
            }
        }

        private void UpdateNavEnabled()
        {
            var ready = _activeProfile != null && _environmentReady && !_isEnvironmentLoading;
            SetNavEnabled(NavBrowser, ready);
            SetNavEnabled(NavRestBuilder, ready);
            SetNavEnabled(NavRequests, ready);
            SetNavEnabled(NavPlugins, ready);
            SetNavEnabled(NavRunner, ready);
        }

        private static void SetNavEnabled(Button? button, bool enabled)
        {
            if (button == null) return;
            button.IsEnabled = enabled;
            if (!enabled)
            {
                button.Tag = null;
                button.ClearValue(Button.BackgroundProperty);
                button.ClearValue(Button.BorderBrushProperty);
                button.ClearValue(Button.ForegroundProperty);
            }
        }

        private async void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_closeAfterCleanup)
            {
                return;
            }

            if (_closeCleanupInProgress)
            {
                e.Cancel = true;
                return;
            }

            var profile = _activeProfile;
            var lockPath = profile != null ? AsyncStepStateService.GetLockFilePath(profile) : null;
            var lockedSteps = !string.IsNullOrWhiteSpace(lockPath) ? AsyncStepStateService.TryReadLockFile(lockPath) : null;

            if (lockedSteps == null || lockedSteps.Count == 0)
            {
                SetWriteModeSilently("FakeWrites");
                SaveAppSettings();
                return;
            }

            e.Cancel = true;
            _closeCleanupInProgress = true;
            var reenabled = await ReenableAsyncStepsForProfileAsync(profile, "Re-enabling async steps...");
            SetWriteModeSilently("FakeWrites");
            SaveAppSettings();
            _closeCleanupInProgress = false;

            if (!reenabled)
            {
                var choice = MessageBox.Show(
                    "Async steps could not be re-enabled. Close anyway?",
                    "Async steps",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (choice != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            _closeAfterCleanup = true;
            Close();
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            _healthTimer.Stop();
            _runnerLogTimer.Stop();
            _runnerLogSettings.PropertyChanged -= OnRunnerLogSettingsChanged;
            _browserSettings.PropertyChanged -= OnBrowserSettingsChanged;
            _runnerSettings.PropertyChanged -= OnRunnerSettingsChanged;
            _appearanceSettings.PropertyChanged -= OnAppearanceSettingsChanged;
            _requestsView.PluginDebuggingChanged -= OnPluginDebuggingChanged;
            _runnerProcess.RunnerExited -= OnRunnerExited;
            _runnerProcess.Stop();
            EnvironmentPathService.CleanupAllRunnerShadowRoots();
        }

        private void OnRunnerExited(object? sender, int pid)
        {
            if (_activeProfile == null)
            {
                return;
            }

            LogService.Append($"Runner exited (pid={pid})");

            Dispatcher.InvokeAsync(async () =>
            {
                if (_activeProfile == null || _isEnvironmentLoading)
                {
                    return;
                }

                if (_healthCheckInProgress)
                {
                    ShowRunnerReloadToast("Runner reloading...");
                    return;
                }

                await HandleRunnerExitedAsync();
            });
        }

        private async Task HandleRunnerExitedAsync()
        {
            if (_runnerReloading || _activeProfile == null)
            {
                return;
            }

            ShowRunnerReloadToast("Runner reloading...");
            _browserView.NotifyRunnerRestarted();
            StopRunnerLogPolling();
            ResetRunnerLogState();
            var started = await EnsureRunnerAsync();
            if (started)
            {
                if (_activeProfile != null)
                {
                    await _runnerView.ApplyEnvironmentAsync(_activeProfile);
                }

                var ready = await WaitForRunnerReadyAsync(TimeSpan.FromSeconds(10));
                if (ready)
                {
                    await ApplyRunnerLogConfigAsync();
                    StartRunnerLogPolling();
                    HideRunnerReloadToast();
                }
            }
            else
            {
                _runnerVm.StatusText = "Runner restart failed.";
                SetBadges(HealthStatus.Error);
            }
        }

        private async void OnRestartRunnerClick(object sender, RoutedEventArgs e)
        {
            if (_activeProfile == null)
            {
                SetRunnerStoppedState();
                return;
            }

            ShowRunnerReloadToast("Runner reloading...");
            _browserView.NotifyRunnerRestarted();
            StopRunnerLogPolling();
            ResetRunnerLogState();
            _runnerProcess.Stop();
            var started = await EnsureRunnerAsync();
            if (started)
            {
                if (_activeProfile != null)
                {
                    await _runnerView.ApplyEnvironmentAsync(_activeProfile);
                }
                StartHealthTimer();
                await ApplyRunnerLogConfigAsync();
                StartRunnerLogPolling();
                await RefreshHealthAsync();
            }
        }

        private async Task<bool> EnsureRunnerAsync()
        {
            try
            {
                var started = await _runnerProcess.EnsureRunningAsync(_runnerSettings.AllowLiveWrites);
                if (!started)
                {
                    _runnerVm.StatusText = "Failed to start runner process.";
                    SetBadges(HealthStatus.Error);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                _runnerVm.StatusText = $"Runner start failed: {ex.Message}";
                SetBadges(HealthStatus.Error);
                return false;
            }
        }

        private void OnNavBrowser(object sender, RoutedEventArgs e) => ShowSection("Browser", NavBrowser);
        private void OnNavRestBuilder(object sender, RoutedEventArgs e) => ShowSection("REST Builder", NavRestBuilder);
        private void OnNavRequests(object sender, RoutedEventArgs e) => ShowSection("Requests", NavRequests);
        private void OnNavPlugins(object sender, RoutedEventArgs e) => ShowSection("Plugins", NavPlugins);
        private void OnNavEnvironments(object sender, RoutedEventArgs e) => ShowSection("Environments", NavEnvironments);
        private void OnNavRunner(object sender, RoutedEventArgs e) => ShowSection("Runner", NavRunner);
        private void OnNavSettings(object sender, RoutedEventArgs e) => ShowSection("Settings", NavSettings);

        private void OnGitHubLinkClick(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void OpenBrowserSection()
        {
            if (!_environmentReady)
            {
                return;
            }
            ShowSection("Browser", NavBrowser);
        }

        private void ShowSection(string section, Button navButton)
        {
            SetNavSelection(navButton);
            SectionTitle.Text = section;
            UIElement? targetView = null;
            switch (section)
            {
                case "Browser":
                    SectionDescription.Text = "Browser with request capture.";
                    targetView = _browserView;
                    break;
                case "REST Builder":
                    SectionDescription.Text = "Compose, organize, and execute Dataverse calls.";
                    targetView = _restBuilderView;
                    _ = EnsureRestBuilderEnvironmentAsync(swallowExceptions: true);
                    break;
                case "Runner":
                    SectionDescription.Text = "Monitor and control the plugin/runner process.";
                    targetView = _runnerView;
                    _ = RefreshHealthAsync();
                    break;
                case "Requests":
                    SectionDescription.Text = "Captured requests and runner responses.";
                    targetView = _requestsView;
                    break;
                case "Plugins":
                    SectionDescription.Text = "Selected plugin registrations for the active environment.";
                    targetView = _pluginsView;
                    break;
                case "Environments":
                    SectionDescription.Text = "Manage Dataverse environments.";
                    targetView = _environmentsView;
                    break;
                case "Settings":
                    SectionDescription.Text = "Application settings and preferences.";
                    targetView = _settingsView;
                    break;
                default:
                    SectionDescription.Text = string.Empty;
                    break;
            }

            if (targetView != null)
            {
                SectionContent.Content = targetView;
            }

            UpdateOverlayWebViewVisibility();
        }

        private async Task EnsureRestBuilderEnvironmentAsync(bool swallowExceptions = false)
        {
            var profile = _activeProfile;
            if (profile == null)
            {
                return;
            }

            if (_restBuilderAppliedProfile != null && ReferenceEquals(_restBuilderAppliedProfile, profile))
            {
                return;
            }

            if (_restBuilderInitTask != null && !_restBuilderInitTask.IsCompleted)
            {
                try
                {
                    await _restBuilderInitTask;
                }
                catch
                {
                    // fall through to retry below
                }

                if (_restBuilderAppliedProfile != null && ReferenceEquals(_restBuilderAppliedProfile, profile))
                {
                    return;
                }
            }

            var initTask = _restBuilderView.ApplyEnvironmentAsync(profile);
            _restBuilderInitTask = initTask;
            try
            {
                await initTask;
                _restBuilderAppliedProfile = profile;
            }
            catch (Exception ex)
            {
                _restBuilderAppliedProfile = null;
                if (swallowExceptions)
                {
                    LogService.Append($"[RestBuilder] Initialization failed: {ex.Message}");
                    return;
                }
                throw;
            }
        }

        private async Task<bool> InjectIntoRestBuilderWhenReadyAsync(RestBuilderInjectionRequest request)
        {
            var deadline = DateTime.UtcNow + RestBuilderInjectionTimeout;
            do
            {
                await EnsureRestBuilderEnvironmentAsync();
                await _restBuilderView.EnsureInjectionHandlerReadyAsync(RestBuilderInjectionTimeout);
                if (await _restBuilderView.InjectCapturedRequestAsync(request))
                {
                    return true;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    break;
                }

                await Task.Delay(RestBuilderInjectionRetryInterval);
            }
            while (true);

            return false;
        }

        private void SetNavSelection(Button selected)
        {
            foreach (var btn in new[] { NavBrowser, NavRestBuilder, NavRequests, NavPlugins, NavEnvironments, NavRunner, NavSettings })
            {
                if (btn == null) continue;
                btn.ClearValue(Button.BackgroundProperty);
                btn.ClearValue(Button.BorderBrushProperty);
                btn.ClearValue(Button.ForegroundProperty);
                if (!btn.IsEnabled)
                {
                    btn.Tag = null;
                    continue;
                }

                btn.Tag = ReferenceEquals(btn, selected) ? "Selected" : null;
            }
        }

        private static string FormatCapabilities(CapabilityFlags capabilities)
        {
            if (capabilities == CapabilityFlags.None)
            {
                return "None";
            }

            var list = new System.Collections.Generic.List<string>();
            if (capabilities.HasFlag(CapabilityFlags.TraceStreaming)) list.Add("Trace streaming");
            if (capabilities.HasFlag(CapabilityFlags.StepCatalog)) list.Add("Step catalog");
            if (capabilities.HasFlag(CapabilityFlags.BatchSupport)) list.Add("$batch");

            return list.Count > 0 ? string.Join(", ", list) : "None";
        }

        private async Task OnEnvironmentActivatedAsync(EnvironmentProfile profile)
        {
            var previousProfile = _activeProfile;
            await ResetLiveWritesForEnvironmentSwitchAsync(previousProfile);
            HideRunnerReloadToast();
            StopRunnerLogPolling();
            ResetRunnerLogState();
            _activeProfile = profile;
            _activeCatalog = null;
            _environmentReady = false;
            _restBuilderAppliedProfile = null;
            _restBuilderInitTask = null;
            SetEnvironmentLoading(true, $"Loading {profile.Name}...", 0);
            UpdateActiveEnvironmentUI();
            UpdateNavEnabled();
            try
            {
                _captureSettings.ApiOnly = profile.CaptureApiOnly;
                _captureSettings.NavigateUrl = string.IsNullOrWhiteSpace(profile.CaptureNavigateUrl) ? profile.OrgUrl : profile.CaptureNavigateUrl;

                SetEnvironmentLoading(true, $"Loading {profile.Name} (1/5) Checking sign-in...", 1);
                var tokenReady = await EnsureTokenAsync(profile);
                if (!tokenReady)
                {
                    return;
                }
                _requestsView.ApplyEnvironment(profile);
                await ReenableAsyncStepsForProfileAsync(
                    profile,
                    $"Loading {profile.Name} (1/5) Re-enabling async steps...",
                    useEnvironmentOverlay: true,
                    environmentStep: 1);

                var metadataReady = false;
                SetEnvironmentLoading(true, $"Loading {profile.Name} (2/5) Fetching metadata...", 2);
                try
                {
                    var meta = await MetadataCacheService.EnsureMetadataAsync(profile, profile.LastAccessToken ?? string.Empty);
                    profile.MetadataFetchedOn = meta.LastUpdatedUtc;
                    _environmentsView.RefreshMetadataStatus(profile);
                    _environmentsView.PersistProfiles();
                    var entityMap = MetadataCacheService.LoadEntitySetMap(profile);
                    _requestsView.ApplyEntityMap(entityMap);
                    _requestsView.ApplyMetadataPath(meta.Path);
                    var operationSnapshotPath = MetadataCacheService.GetOperationParametersPath(profile);
                    _requestsView.ApplyOperationSnapshot(operationSnapshotPath);
                    metadataReady = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Metadata fetch failed: {ex.Message}", "Metadata", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                var catalogReady = false;
                SetEnvironmentLoading(true, $"Loading {profile.Name} (3/5) Fetching plugin catalog...", 3);
                try
                {
                    PluginCatalog? catalog = null;
                    try
                    {
                        catalog = await PluginCatalogService.LoadCatalogAsync(profile);
                    }
                    catch (Exception ex)
                    {
                        LogService.Append($"Catalog cache read failed: {ex.Message}");
                        catalog = null;
                    }

                    if (catalog != null)
                    {
                        LogService.Append($"Is catalog filtered: {catalog.IsFiltered}");
                        if (catalog.IsFiltered)
                        {
                            LogService.Append("Catalog cache looks filtered; refreshing full catalog.");
                            catalog = null;
                        }
                    }

                    if (catalog == null)
                    {
                        catalog = await PluginCatalogService.RefreshCatalogAsync(profile, profile.LastAccessToken ?? string.Empty);
                    }
                    else
                    {
                        SetEnvironmentLoading(true, $"Loading {profile.Name} (3/5) Using cached plugin catalog...", 3);
                    }

                    profile.PluginCatalogFetchedOn = catalog.FetchedOnUtc;
                    _environmentsView.RefreshCatalogStatus(profile);
                    _environmentsView.PersistProfiles();
                    _requestsView.ApplyAssemblyPaths(profile.PluginAssemblies);
                    _runnerView.ApplyCatalog(catalog, profile.PluginAssemblies);
                    _requestsView.ApplyCatalog(catalog);
                    _pluginsView.ApplyCatalog(catalog, profile.PluginAssemblies);
                    _activeCatalog = catalog;
                    catalogReady = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Plugin catalog fetch failed: {ex.Message}", "Plugin catalog", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _pluginsView.ShowCatalogUnavailable(ex.Message);
                }

                if (!metadataReady || !catalogReady)
                {
                    return;
                }

                SetEnvironmentLoading(true, $"Loading {profile.Name} (4/5) Starting runner...", 4);
                _browserView.NotifyRunnerRestarted();
                _runnerProcess.Stop();
                EnvironmentPathService.CleanupAllRunnerShadowRoots();
                if (await EnsureRunnerAsync())
                {
                    await _runnerView.ApplyEnvironmentAsync(profile);
                    SetEnvironmentLoading(true, $"Loading {profile.Name} (4/5) Waiting for runner...", 4);
                    var ready = await WaitForRunnerReadyAsync(TimeSpan.FromSeconds(10));
                    if (!ready)
                    {
                        MessageBox.Show("Runner did not become ready in time.", "Runner", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    StartHealthTimer();
                    await ApplyRunnerLogConfigAsync();
                    StartRunnerLogPolling();
                }

                SetEnvironmentLoading(true, $"Loading {profile.Name} (5/5) Initializing browser...", 5);
                _ = _browserView.ApplyEnvironmentAsync(_captureSettings.NavigateUrl, _captureSettings.ApiOnly, _captureSettings.AutoProxy, profile.WebViewCachePath);
                _browserView.SetEnvironmentContext(profile, profile.LastAccessToken);
                if (_browserSettings.OpenDevToolsOnActivate)
                {
                    _browserView.RequestOpenDevToolsOnReady();
                }
                _environmentReady = true;
                UpdateNavEnabled();
                ShowSection("Browser", NavBrowser);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Environment activation failed: {ex.Message}", "Activation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetEnvironmentLoading(false, null, null);
                if (_environmentReady)
                {
                    SetNavSelection(NavBrowser);
                }
            }
        }

        private async Task OnEnvironmentDeactivatedAsync(EnvironmentProfile profile)
        {
            if (_activeProfile == null || !ReferenceEquals(_activeProfile, profile))
            {
                return;
            }

            var previousProfile = _activeProfile;
            await ResetLiveWritesForEnvironmentSwitchAsync(previousProfile);
            _activeCatalog = null;
            _environmentReady = false;
            _isEnvironmentLoading = false;
            _activeProfile = null;
            _restBuilderAppliedProfile = null;
            _restBuilderInitTask = null;
            SetEnvironmentLoading(false, null, null);
            SetRunnerStoppedState();
            EnvironmentPathService.CleanupAllRunnerShadowRoots();
            _pluginsView.Clear();
            UpdateActiveEnvironmentUI();
            UpdateNavEnabled();
            ShowSection("Environments", NavEnvironments);
        }

        private void ShowRunnerReloadToast(string? message)
        {
            _runnerReloading = true;
            _runnerReloadShownAtUtc = DateTime.UtcNow;
            if (_runnerReloadHideTimer != null)
            {
                _runnerReloadHideTimer.Stop();
                _runnerReloadHideTimer = null;
            }
            if (EnvironmentLoadingOverlay == null)
            {
                return;
            }
            LogService.Append($"Runner reload overlay show (envLoading={_isEnvironmentLoading})");
            if (!string.IsNullOrWhiteSpace(message) && EnvironmentLoadingText != null && !_isEnvironmentLoading)
            {
                EnvironmentLoadingText.Text = message;
            }
            if (!_isEnvironmentLoading)
            {
                UpdateLoadingSteps(4);
            }
            if (EnvironmentLoadingStepsPanel != null && !_isEnvironmentLoading)
            {
                EnvironmentLoadingStepsPanel.Visibility = Visibility.Collapsed;
            }
            EnvironmentLoadingOverlay.Visibility = Visibility.Visible;
            UpdateOverlayWebViewVisibility();
        }

        private void HideRunnerReloadToast()
        {
            HideRunnerReloadToastInternal(false);
        }

        private void HideRunnerReloadToastInternal(bool force)
        {
            _runnerReloading = false;
            if (_isEnvironmentLoading || EnvironmentLoadingOverlay == null)
            {
                return;
            }

            if (!force && _runnerReloadShownAtUtc.HasValue)
            {
                var elapsed = DateTime.UtcNow - _runnerReloadShownAtUtc.Value;
                if (elapsed < _runnerReloadMinVisible)
                {
                    var remaining = _runnerReloadMinVisible - elapsed;
                    if (_runnerReloadHideTimer == null)
                    {
                        _runnerReloadHideTimer = new DispatcherTimer();
                        _runnerReloadHideTimer.Tick += (_, _) =>
                        {
                            _runnerReloadHideTimer?.Stop();
                            _runnerReloadHideTimer = null;
                            HideRunnerReloadToastInternal(true);
                        };
                    }
                    _runnerReloadHideTimer.Interval = remaining;
                    _runnerReloadHideTimer.Start();
                    return;
                }
            }

            EnvironmentLoadingOverlay.Visibility = Visibility.Collapsed;
            _runnerReloadShownAtUtc = null;
            LogService.Append("Runner reload overlay hide");
            UpdateOverlayWebViewVisibility();
        }

        private void UpdateActiveEnvironmentUI()
        {
            var envName = _activeProfile?.Name;
            Title = string.IsNullOrWhiteSpace(envName)
                ? BaseWindowTitle
                : $"{BaseWindowTitle} - {envName}";
        }

        private void SetEnvironmentLoading(bool isLoading, string? message, int? step)
        {
            _isEnvironmentLoading = isLoading;
            if (EnvironmentLoadingOverlay != null)
            {
                EnvironmentLoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            }
            if (EnvironmentLoadingStepsPanel != null && isLoading)
            {
                EnvironmentLoadingStepsPanel.Visibility = Visibility.Visible;
            }
            if (EnvironmentLoadingText != null && !string.IsNullOrWhiteSpace(message))
            {
                EnvironmentLoadingText.Text = message;
            }
            if (isLoading)
            {
                UpdateLoadingSteps(step ?? 0);
            }
            UpdateNavEnabled();
            UpdateOverlayWebViewVisibility();
        }

        private void UpdateOverlayWebViewVisibility()
        {
            var isBrowser = SectionContent?.Content == _browserView;
            var isRestBuilder = SectionContent?.Content == _restBuilderView;
            if (!isBrowser && !isRestBuilder)
            {
                return;
            }

            var shouldHide = _isEnvironmentLoading || _runnerReloading;
            if (shouldHide)
            {
                if (isBrowser)
                {
                    _browserView.SetWebViewVisibility(false);
                }
                else
                {
                    _restBuilderView.SetWebViewVisibility(false);
                }
                _webViewHiddenForOverlay = true;
                return;
            }

            if (_webViewHiddenForOverlay)
            {
                if (isBrowser)
                {
                    _browserView.SetWebViewVisibility(true);
                }
                else
                {
                    _restBuilderView.SetWebViewVisibility(true);
                }
                _webViewHiddenForOverlay = false;
            }
        }

        private void UpdateLoadingSteps(int currentStep)
        {
            SetLoadingStep(1, currentStep, LoadingStep1Icon, LoadingStep1Text);
            SetLoadingStep(2, currentStep, LoadingStep2Icon, LoadingStep2Text);
            SetLoadingStep(3, currentStep, LoadingStep3Icon, LoadingStep3Text);
            SetLoadingStep(4, currentStep, LoadingStep4Icon, LoadingStep4Text);
            SetLoadingStep(5, currentStep, LoadingStep5Icon, LoadingStep5Text);
        }

        private static void SetLoadingStep(int stepNumber, int currentStep, TextBlock? iconBlock, TextBlock? textBlock)
        {
            if (iconBlock == null || textBlock == null)
            {
                return;
            }

            string icon;
            Brush color;

            if (stepNumber < currentStep)
            {
                icon = "[✓]";
                color = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            }
            else if (stepNumber == currentStep)
            {
                icon = "[>]";
                color = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
            }
            else
            {
                icon = "[ ]";
                color = new SolidColorBrush(Color.FromRgb(0x90, 0xA4, 0xAE));
            }

            iconBlock.Text = icon;
            iconBlock.Foreground = color;
            textBlock.Foreground = color;
        }

        private async Task<bool> EnsureTokenAsync(EnvironmentProfile profile)
        {
            var needsLogin = string.IsNullOrWhiteSpace(profile.LastAccessToken) ||
                             profile.AccessTokenExpiresOn == null ||
                             profile.AccessTokenExpiresOn <= DateTimeOffset.UtcNow.AddMinutes(2);

            if (needsLogin)
            {
                try
                {
                    var result = await AuthService.AcquireTokenInteractiveAsync(profile);
                    if (result == null)
                    {
                        return false;
                    }

                    profile.LastAccessToken = result.AccessToken;
                    profile.SignedInUser = result.User;
                    profile.AccessTokenExpiresOn = result.ExpiresOn;
                    _environmentsView.RefreshAuthStatus(profile);
                    _environmentsView.PersistProfiles();
                    if (_browserView != null && ReferenceEquals(profile, _activeProfile))
                    {
                        _browserView.SetEnvironmentContext(profile, profile.LastAccessToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show($"Sign-in canceled for {profile.Name}.", "Authentication", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Sign-in required for {profile.Name}: {ex.Message}", "Authentication", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> WaitForRunnerReadyAsync(TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var health = await _runnerClient.CheckHealthAsync(timeout: HealthCheckTimeout);
                    _runnerVm.StatusText = health.Message ?? $"Status: {health.Status}";
                    _runnerVm.CapabilitiesText = FormatCapabilities(health.Capabilities);
                    SetBadges(health.Status);
                    if (health.Status == HealthStatus.Ready)
                    {
                        if (_runnerReloading)
                        {
                            HideRunnerReloadToast();
                        }
                        return true;
                    }
                }
                catch
                {
                    _runnerVm.StatusText = "Checking runner...";
                    SetBadges(HealthStatus.Unknown);
                }

                await Task.Delay(300);
            }

            return false;
        }

        private void OnRunnerSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressRunnerSettingsHandling)
            {
                return;
            }

            if (string.Equals(e.PropertyName, nameof(RunnerSettingsModel.ExecutionMode), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(RunnerSettingsModel.AllowLiveWrites), StringComparison.Ordinal))
            {
                _ = HandleExecutionSettingsChangedAsync();
            }
        }

        private async Task HandleExecutionSettingsChangedAsync()
        {
            var executionMode = _runnerSettings.ExecutionMode;
            var allowLiveWrites = _runnerSettings.AllowLiveWrites;

            if (string.Equals(executionMode, _lastExecutionMode, StringComparison.OrdinalIgnoreCase) &&
                allowLiveWrites == _lastAllowLiveWrites)
            {
                return;
            }

            var previousExecutionMode = _lastExecutionMode;
            var previousAllowLiveWrites = _lastAllowLiveWrites;
            _lastExecutionMode = executionMode;
            _lastAllowLiveWrites = allowLiveWrites;

            var isOnline = IsExecutionModeOnline(executionMode);
            var wasLiveWrites = IsExecutionModeOnline(previousExecutionMode) && previousAllowLiveWrites;
            var isLiveWrites = isOnline && allowLiveWrites;

            if (!isOnline && allowLiveWrites)
            {
                if (wasLiveWrites)
                {
                    await ReenableAsyncStepsForProfileAsync(_activeProfile, "Re-enabling async steps...");
                }

                SetExecutionModeSilently(executionMode, allowLiveWrites: false);
                SaveAppSettings();
                await RestartRunnerForSettingsChangeAsync();
                return;
            }

            if (isLiveWrites && !wasLiveWrites)
            {
                var proceed = MessageBox.Show(
                    "Live writes will disable async steps for the loaded plugin assemblies in the active environment. Continue?",
                    "Live writes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes)
                {
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        SetExecutionModeSilently("Hybrid", allowLiveWrites: false);
                        SaveAppSettings();
                    }, DispatcherPriority.Background);
                    return;
                }

                var disabled = await DisableAsyncStepsForActiveEnvironmentAsync();
                if (!disabled)
                {
                    SetExecutionModeSilently(executionMode, allowLiveWrites: false);
                    SaveAppSettings();
                    return;
                }
            }
            else if (!isLiveWrites && wasLiveWrites)
            {
                await ReenableAsyncStepsForProfileAsync(_activeProfile, "Re-enabling async steps...");
            }

            SaveAppSettings();
            if (_runnerSettings.AllowLiveWrites != previousAllowLiveWrites)
            {
                await RestartRunnerForSettingsChangeAsync();
            }
        }

        private async Task<bool> DisableAsyncStepsForActiveEnvironmentAsync()
        {
            if (_activeProfile == null)
            {
                MessageBox.Show("Activate an environment before enabling live writes.", "Live writes", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            if (_activeCatalog == null)
            {
                MessageBox.Show("Plugin catalog not loaded. Activate the environment first.", "Live writes", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var lockPath = AsyncStepStateService.GetLockFilePath(_activeProfile);
            var existing = AsyncStepStateService.TryReadLockFile(lockPath);
            if (existing != null && existing.Count > 0)
            {
                return true;
            }

            var tokenReady = await EnsureTokenAsync(_activeProfile);
            if (!tokenReady || string.IsNullOrWhiteSpace(_activeProfile.LastAccessToken))
            {
                MessageBox.Show("Sign-in required to disable async steps.", "Live writes", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var stepIds = AsyncStepStateService.ResolveAsyncStepIds(_activeCatalog, _activeProfile.PluginAssemblies);
            if (stepIds.Count == 0)
            {
                LogService.Append("No async steps found for the selected plugin assemblies.");
                return true;
            }

            ShowRunnerReloadToast("Disabling async steps...");
            try
            {
                var result = await AsyncStepStateService.SetAsyncStepsEnabledAsync(_activeProfile, _activeProfile.LastAccessToken, stepIds, enabled: false);
                if (result.Succeeded.Count > 0)
                {
                    AsyncStepStateService.WriteLockFile(lockPath, result.Succeeded);
                }

                if (result.Failed.Count > 0)
                {
                    MessageBox.Show($"Disabled {result.Succeeded.Count} async steps, {result.Failed.Count} failed.", "Async steps", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return result.Succeeded.Count > 0;
            }
            finally
            {
                HideRunnerReloadToast();
            }
        }

        private async Task<bool> ReenableAsyncStepsForProfileAsync(EnvironmentProfile? profile, string message, bool useEnvironmentOverlay = false, int environmentStep = 1)
        {
            if (profile == null)
            {
                return true;
            }

            var lockPath = AsyncStepStateService.GetLockFilePath(profile);
            var stepIds = AsyncStepStateService.TryReadLockFile(lockPath);
            if (stepIds == null || stepIds.Count == 0)
            {
                return true;
            }

            var tokenReady = await EnsureTokenAsync(profile);
            if (!tokenReady || string.IsNullOrWhiteSpace(profile.LastAccessToken))
            {
                MessageBox.Show("Sign-in required to re-enable async steps.", "Async steps", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var showEnvironmentOverlay = useEnvironmentOverlay && _isEnvironmentLoading;
            if (showEnvironmentOverlay)
            {
                SetEnvironmentLoading(true, message, environmentStep);
            }
            else
            {
                ShowRunnerReloadToast(message);
            }
            try
            {
                var result = await AsyncStepStateService.SetAsyncStepsEnabledAsync(profile, profile.LastAccessToken, stepIds, enabled: true);
                if (result.Failed.Count == 0)
                {
                    AsyncStepStateService.DeleteLockFile(lockPath);
                }
                else
                {
                    AsyncStepStateService.WriteLockFile(lockPath, result.Failed);
                    MessageBox.Show($"Re-enabled {result.Succeeded.Count} async steps, {result.Failed.Count} failed.", "Async steps", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return result.Failed.Count == 0;
            }
            finally
            {
                if (!showEnvironmentOverlay)
                {
                    HideRunnerReloadToast();
                }
            }
        }

        private async Task ResetLiveWritesForEnvironmentSwitchAsync(EnvironmentProfile? previousProfile)
        {
            await ReenableAsyncStepsForProfileAsync(previousProfile, "Re-enabling async steps...");
            SetWriteModeSilently("FakeWrites");
            SaveAppSettings();
        }

        private async Task RestartRunnerForSettingsChangeAsync()
        {
            if (_activeProfile == null || !_environmentReady || _isEnvironmentLoading)
            {
                return;
            }

            ShowRunnerReloadToast("Runner reloading...");
            _browserView.NotifyRunnerRestarted();
            StopRunnerLogPolling();
            ResetRunnerLogState();
            _runnerProcess.Stop();

            var started = await EnsureRunnerAsync();
            if (!started)
            {
                HandleRunnerRestartFailure();
                return;
            }

            await _runnerView.ApplyEnvironmentAsync(_activeProfile);
            var ready = await WaitForRunnerReadyAsync(TimeSpan.FromSeconds(10));
            if (!ready)
            {
                HandleRunnerRestartFailure();
                return;
            }

            StartHealthTimer();
            await ApplyRunnerLogConfigAsync();
            StartRunnerLogPolling();
            HideRunnerReloadToast();
        }

        private void HandleRunnerRestartFailure()
        {
            HideRunnerReloadToast();
            _runnerVm.StatusText = "Runner restart failed.";
            SetBadges(HealthStatus.Error);

            SetExecutionModeSilently("Hybrid", allowLiveWrites: false);
            SaveAppSettings();
            MessageBox.Show(
                "Runner restart failed. Execution mode reverted to Hybrid and live writes were disabled.",
                "Runner",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void SetWriteModeSilently(string mode)
        {
            _suppressRunnerSettingsHandling = true;
            _runnerSettings.WriteMode = mode;
            _lastExecutionMode = _runnerSettings.ExecutionMode;
            _lastAllowLiveWrites = _runnerSettings.AllowLiveWrites;
            _suppressRunnerSettingsHandling = false;
        }

        private void SetExecutionModeSilently(string mode, bool allowLiveWrites)
        {
            _suppressRunnerSettingsHandling = true;
            _runnerSettings.ExecutionMode = mode;
            _runnerSettings.AllowLiveWrites = allowLiveWrites;
            _lastExecutionMode = _runnerSettings.ExecutionMode;
            _lastAllowLiveWrites = _runnerSettings.AllowLiveWrites;
            _suppressRunnerSettingsHandling = false;
        }

        private static bool IsExecutionModeOnline(string? mode)
        {
            return string.Equals(mode, "Online", StringComparison.OrdinalIgnoreCase);
        }

        private void OnRunnerLogSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            SaveAppSettings();
            if (_activeProfile == null || !_environmentReady)
            {
                return;
            }

            _ = ApplyRunnerLogConfigAsync();
        }

        private void OnBrowserSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            SaveAppSettings();
            _ = _browserView.ApplyBrowserSettingsAsync();
        }

        private void OnAppearanceSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppearanceSettingsModel.IsDarkMode))
            {
                ThemeService.IsDarkMode = _appearanceSettings.IsDarkMode;
                SaveAppSettings();
            }
        }

        private void SaveAppSettings()
        {
            var model = new AppSettingsModel();
            model.Browser.DisableCaching = _browserSettings.DisableCaching;
            model.Browser.BypassServiceWorker = _browserSettings.BypassServiceWorker;
            model.Browser.OpenDevToolsOnActivate = _browserSettings.OpenDevToolsOnActivate;
            model.RunnerLog.Level = _runnerLogSettings.Level;
            model.RunnerLog.ApplyCategories(_runnerLogSettings.ToCategories());
            model.Runner.ExecutionMode = _defaultExecutionMode;
            model.Runner.AllowLiveWrites = _defaultAllowLiveWrites;
            model.Runner.WriteMode = _defaultAllowLiveWrites &&
                                     string.Equals(_defaultExecutionMode, "Online", StringComparison.OrdinalIgnoreCase)
                ? "LiveWrites"
                : "FakeWrites";
            model.Appearance.IsDarkMode = _appearanceSettings.IsDarkMode;
            AppSettingsService.Save(model);
        }

        private async Task ApplyRunnerLogConfigAsync()
        {
            if (_runnerLogConfigInProgress || _activeProfile == null || !_environmentReady)
            {
                return;
            }

            _runnerLogConfigInProgress = true;
            try
            {
                var request = new RunnerLogConfigRequest
                {
                    Level = _runnerLogSettings.Level,
                    Categories = _runnerLogSettings.ToCategories(),
                    MaxEntries = 1200
                };

                var response = await _runnerClient.UpdateRunnerLogConfigAsync(request, TimeSpan.FromSeconds(2));
                if (!response.Applied && !string.IsNullOrWhiteSpace(response.Message))
                {
                    LogService.Append($"Runner log config failed: {response.Message}");
                }
            }
            finally
            {
                _runnerLogConfigInProgress = false;
            }
        }

        private async Task FetchRunnerLogsAsync()
        {
            if (_runnerLogPollInProgress || _activeProfile == null || !_environmentReady)
            {
                return;
            }

            _runnerLogPollInProgress = true;
            try
            {
                var response = await _runnerClient.FetchRunnerLogsAsync(
                    new RunnerLogFetchRequest
                    {
                        LastId = _runnerLogLastId,
                        MaxEntries = 200
                    },
                    TimeSpan.FromSeconds(2));

                if (response?.Lines == null || response.Lines.Count == 0)
                {
                    return;
                }

                _runnerLogLastId = Math.Max(_runnerLogLastId, response.LastId);
                AppendRunnerTraceLines(response.Lines);
            }
            finally
            {
                _runnerLogPollInProgress = false;
            }
        }

        private static bool IsCatalogFiltered(PluginCatalog catalog, IEnumerable<string>? selectedAssemblyPaths)
        {
            if (catalog == null || selectedAssemblyPaths == null)
            {
                return false;
            }

            var selectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in selectedAssemblyPaths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var file = System.IO.Path.GetFileName(path);
                if (!string.IsNullOrWhiteSpace(file))
                {
                    selectedNames.Add(file);
                }

                var fileNoExt = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrWhiteSpace(fileNoExt))
                {
                    selectedNames.Add(fileNoExt);
                }
            }

            if (selectedNames.Count == 0 || catalog.Assemblies == null || catalog.Assemblies.Count == 0)
            {
                return false;
            }

            return catalog.Assemblies.All(a =>
            {
                if (string.IsNullOrWhiteSpace(a.Name))
                {
                    return false;
                }

                if (selectedNames.Contains(a.Name))
                {
                    return true;
                }

                var noExt = System.IO.Path.GetFileNameWithoutExtension(a.Name);
                return !string.IsNullOrWhiteSpace(noExt) && selectedNames.Contains(noExt);
            });
        }

        private void AppendRunnerTraceLines(IEnumerable<string> lines)
        {
            if (lines == null)
            {
                return;
            }

            var buffer = new List<string>();
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    buffer.Add(line);
                }
            }

            if (buffer.Count == 0)
            {
                return;
            }

            void Append()
            {
                foreach (var line in buffer)
                {
                    _runnerVm.TraceLines.Add(line);
                }

                const int maxLines = 1500;
                while (_runnerVm.TraceLines.Count > maxLines)
                {
                    _runnerVm.TraceLines.RemoveAt(0);
                }
            }

            if (Dispatcher.CheckAccess())
            {
                Append();
            }
            else
            {
                Dispatcher.Invoke(Append);
            }
        }

        private void ResetRunnerLogState()
        {
            _runnerLogLastId = 0;
            if (Dispatcher.CheckAccess())
            {
                _runnerVm.TraceLines.Clear();
            }
            else
            {
                Dispatcher.Invoke(() => _runnerVm.TraceLines.Clear());
            }
        }

        private void StartRunnerLogPolling()
        {
            if (!_runnerLogTimer.IsEnabled)
            {
                _runnerLogTimer.Start();
            }
        }

        private void StopRunnerLogPolling()
        {
            if (_runnerLogTimer.IsEnabled)
            {
                _runnerLogTimer.Stop();
            }
        }

    }
}
