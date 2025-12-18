using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Security.Cryptography;
using System.Text;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Runner;
using DataverseDebugger.Protocol;
using DataverseDebugger.App.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Windows.Threading;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View hosting an embedded WebView2 browser for interacting with Dataverse model-driven apps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Captures HTTP requests, supports auto-proxying to the local runner, and integrates
    /// with the Visual Studio debugger for plugin debugging.
    /// </para>
    /// <para>
    /// Includes form tools (logical names, god mode, etc.) and user impersonation support
    /// for Web API calls using the MSCRMCallerID header.
    /// </para>
    /// </remarks>
    public partial class BrowserView : UserControl
    {
        private static readonly string[] AllowedSchemes = { Uri.UriSchemeHttp, Uri.UriSchemeHttps };
        private static readonly int MaxItems = 500;
        private static readonly int MaxBodyBytes = 4096;
        private static readonly int ProxyDedupWindowMs = 2000;

        private readonly RunnerClient _runnerClient;
        private readonly ObservableCollection<CapturedRequest> _requests;
        private readonly ObservableCollection<string> _globalTrace;
        private readonly CaptureSettingsModel _settings;
        private readonly BrowserSettingsModel _browserSettings;
        private readonly System.Collections.Generic.Dictionary<string, DateTime> _recentRequests = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.Dictionary<string, DateTime> _recentProxies = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _proxyInFlight = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.Dictionary<string, Task<ExecuteResponse>> _proxyTasks = new(System.StringComparer.OrdinalIgnoreCase);
        private VisualStudioInstance? _attachedDebugger;
        private bool _suppressDebugToggle;
        private readonly DispatcherTimer _debugMonitorTimer = new DispatcherTimer();
        private bool _pendingDevToolsOpen;
        public Func<CapturedRequest, Task>? BeforeAutoProxyAsync { get; set; }
        public Func<CapturedRequest, ExecuteResponse, Task>? AfterAutoProxyAsync { get; set; }
        public event Action<bool>? DebugStateChanged;

        private bool _webViewReady;
        private bool _initialized;
        private WebView2? _webView;
        private string? _userDataFolder;
        private string? _currentUserDataFolder;
        private bool _suppressUrlTextChanged;

        // Impersonation state
        private DataverseUser? _impersonatedUser;
        private EnvironmentProfile? _currentProfile;
        private string? _currentAccessToken;

        public BrowserView(RunnerClient runnerClient, ObservableCollection<CapturedRequest> requests, ObservableCollection<string> globalTrace, CaptureSettingsModel settings, BrowserSettingsModel browserSettings)
        {
            _runnerClient = runnerClient;
            _requests = requests;
            _globalTrace = globalTrace;
            _settings = settings;
            _browserSettings = browserSettings;
            InitializeComponent();
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            _browserSettings.PropertyChanged += OnBrowserSettingsPropertyChanged;
            _debugMonitorTimer.Interval = TimeSpan.FromSeconds(1);
            _debugMonitorTimer.Tick += OnDebugMonitorTick;
            Loaded += OnLoaded;
            CaptureToggle.IsChecked = _settings.CaptureEnabled;
            AutoProxyToggle.IsChecked = _settings.AutoProxy;
            _suppressDebugToggle = true;
            DebugToggle.IsChecked = _settings.AutoDebugMatched;
            _suppressDebugToggle = false;
            if (string.IsNullOrWhiteSpace(_settings.NavigateUrl))
            {
                _settings.NavigateUrl = NavigateUrl.Text;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            _initialized = true;
            CreateWebViewControl();
            await EnsureWebViewAsync();
        }

        private async Task EnsureWebViewAsync()
        {
            try
            {
                if (_webView == null)
                {
                    CreateWebViewControl();
                }

                CoreWebView2Environment? env = null;
                if (!string.IsNullOrWhiteSpace(_userDataFolder))
                {
                    Directory.CreateDirectory(_userDataFolder);
                    env = await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataFolder);
                }

                if (env != null)
                {
                    await _webView!.EnsureCoreWebView2Async(env);
                }
                else
                {
                    await _webView!.EnsureCoreWebView2Async();
                }

                if (_webView.CoreWebView2 != null && !_webViewReady)
                {
                    _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                    _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
                    _webView.CoreWebView2.HistoryChanged += OnHistoryChanged;
                    _webView.CoreWebView2.SourceChanged += OnSourceChanged;
                    _webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                    _webView.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
                    _webView.CoreWebView2.WebResourceResponseReceived += OnWebResourceResponseReceived;
                    _webViewReady = true;
                    _currentUserDataFolder = _userDataFolder;
                    UpdateNavButtons();
                    UpdateNavigateUrl();
                    TryOpenDevToolsIfReady();
                }
                NavigateToDefault();
                QueueApplyBrowserSettings();
            }
            catch (Exception)
            {
                // swallow UI status for now
            }
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateNavigateUrl();
            UpdateNavButtons();
        }

        private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            UpdateNavButtons();
        }

        private void OnHistoryChanged(object? sender, object e)
        {
            UpdateNavButtons();
        }

        private void OnSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
        {
            UpdateNavigateUrl();
        }

        private async void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (!_settings.CaptureEnabled)
            {
                return;
            }

            var method = e.Request.Method;
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                // skip CORS preflight noise to avoid duplicate entries
                return;
            }

            var originalUrl = e.Request.Uri;
            var sanitizedUrl = SanitizeUrl(originalUrl);
            if (!IsAllowed(originalUrl, _settings.ApiOnly))
            {
                return;
            }

            var bodySnippet = ReadRequestBody(e, out var bodyBytes);
            var (headersText, rawHeadersMap) = FlattenHeaders(e.Request.Headers);
            var clientRequestId = TryGetHeader(rawHeadersMap, "x-ms-client-request-id");
            var bodyHash = ComputeBodyHash(bodyBytes);

            if (IsDuplicate(method, originalUrl, clientRequestId, bodyHash))
            {
                return;
            }

            var item = new CapturedRequest
            {
                Method = method,
                OriginalUrl = originalUrl,
                Url = sanitizedUrl,
                Headers = headersText,
                HeadersDictionary = rawHeadersMap,
                BodyPreview = bodySnippet,
                Body = bodyBytes,
                ClientRequestId = clientRequestId,
                Timestamp = DateTimeOffset.Now,
                ResponseStatus = 0
            };

            _requests.Add(item);
            if (_requests.Count > MaxItems)
            {
                _requests.RemoveAt(0);
            }

            if (_settings.AutoProxy && IsSafeForAutoProxy(method))
            {
                item.AutoProxied = true;
                if (ShouldIntercept(method))
                {
                    var deferral = e.GetDeferral();
                    try
                    {
                        var response = await ExecuteRunnerAsync(item).ConfigureAwait(true);
                        var webResponse = BuildWebViewResponse(response);
                        if (webResponse != null)
                        {
                            e.Response = webResponse;
                        }
                    }
                    finally
                    {
                        deferral.Complete();
                    }
                    return;
                }

                _ = ProxyToRunnerAsync(item);
            }
        }

        private static (string Flattened, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> RawMap) FlattenHeaders(CoreWebView2HttpRequestHeaders headers)
        {
            try
            {
                var lines = new System.Text.StringBuilder();
                var rawMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in headers)
                {
                    if (!rawMap.TryGetValue(header.Key, out var list))
                    {
                        list = new System.Collections.Generic.List<string>();
                        rawMap[header.Key] = list;
                    }
                    list.Add(header.Value);

                    var displayValue = SanitizeHeaderValue(header.Key, header.Value);

                    if (string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        lines.AppendLine($"{header.Key}: <redacted>");
                    }
                    else
                    {
                        lines.AppendLine($"{header.Key}: {displayValue}");
                    }
                }
                return (lines.Length > 0 ? lines.ToString() : "(none)", rawMap);
            }
            catch
            {
                return ("(headers unavailable)", new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase));
            }
        }

        private static string ReadRequestBody(CoreWebView2WebResourceRequestedEventArgs e, out byte[] bodyBytes)
        {
            const int previewLimit = 4096;
            try
            {
                var content = e.Request.Content;
                if (content == null)
                {
                    bodyBytes = Array.Empty<byte>();
                    return "(no body)";
                }

                var totalLength = (int)content.Length;
                var allBytes = new byte[totalLength];
                var read = content.Read(allBytes, 0, totalLength);
                if (read <= 0)
                {
                    bodyBytes = Array.Empty<byte>();
                    return "(empty body)";
                }

                bodyBytes = allBytes;
                var previewLen = Math.Min(read, previewLimit);
                var snippet = System.Text.Encoding.UTF8.GetString(allBytes, 0, previewLen);
                if (read > previewLimit)
                {
                    snippet += $" ... (+{read - previewLimit} bytes)";
                }
                return snippet;
            }
            catch
            {
                bodyBytes = Array.Empty<byte>();
                return "(body unavailable)";
            }
        }

        private static bool IsAllowed(string url, bool apiOnly)
        {
            try
            {
                var uri = new Uri(url);
                if (Array.IndexOf(AllowedSchemes, uri.Scheme) < 0)
                {
                    return false;
                }

                return apiOnly ? IsApiPath(uri) : true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsApiPath(Uri uri)
        {
            var path = uri.AbsolutePath.ToLowerInvariant();
            return path.Contains("/api/");
        }

        private static string SanitizeUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var builder = new UriBuilder(uri)
                {
                    Query = string.Empty // strip query for UI list
                };
                return builder.Uri.ToString();
            }
            catch
            {
                return url;
            }
        }

        private static string SanitizeHeaderValue(string name, string value)
        {
            if (string.Equals(name, "Cookie", StringComparison.OrdinalIgnoreCase))
            {
                return "<redacted>";
            }
            return value;
        }

        private bool IsDuplicate(string method, string url, string? clientRequestId, string bodyHash)
        {
            // Capture dedup disabled so we can see every request; proxy layer still dedups.
            return false;
        }

        private static string ComputeBodyHash(byte[]? body)
        {
            if (body == null || body.Length == 0) return "empty";
            using var sha = SHA256.Create();
            var len = Math.Min(body.Length, 4096); // hash more to reduce collisions
            var hash = sha.ComputeHash(body, 0, len);
            return Convert.ToHexString(hash);
        }

        private static bool IsSafeForAutoProxy(string method)
        {
            // Re-enable for CRUD so we can proxy writes again
            return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldIntercept(string method)
        {
            // Intercept non-read requests so we don't double-send writes.
            return !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
        }

        private void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                var req = e.Request;
                var method = req.Method;
                var url = req.Uri;
                var status = e.Response.StatusCode;

                // Find latest matching captured request without a status
                var match = _requests
                    .Where(r => string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(r.OriginalUrl, url, StringComparison.OrdinalIgnoreCase) &&
                                (!r.ResponseStatus.HasValue || r.ResponseStatus == 0))
                    .LastOrDefault();

                if (match != null)
                {
                    match.ResponseStatus = status;
                    // Refresh UI
                    Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void OnNavigateCustom(object sender, RoutedEventArgs e)
        {
            NavigateToDefault();
        }

        private void OnUrlKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter)
            {
                return;
            }

            e.Handled = true;
            NavigateToDefault();
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            var core = _webView?.CoreWebView2;
            if (core == null || !core.CanGoBack) return;
            core.GoBack();
        }

        private void OnForwardClick(object sender, RoutedEventArgs e)
        {
            var core = _webView?.CoreWebView2;
            if (core == null || !core.CanGoForward) return;
            core.GoForward();
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            _webView?.CoreWebView2?.Reload();
        }

        private void OnDevToolsClick(object sender, RoutedEventArgs e)
        {
            _webView?.CoreWebView2?.OpenDevToolsWindow();
        }

        private void NavigateToDefault()
        {
            var target = NormalizeUrl(NavigateUrl.Text);
            if (!string.IsNullOrWhiteSpace(target))
            {
                if (!string.Equals(NavigateUrl.Text, target, StringComparison.OrdinalIgnoreCase))
                {
                    _suppressUrlTextChanged = true;
                    NavigateUrl.Text = target;
                    _suppressUrlTextChanged = false;
                }
                _webView?.CoreWebView2?.Navigate(target);
            }
        }

        private void UpdateNavButtons()
        {
            var core = _webView?.CoreWebView2;
            var ready = core != null;
            if (BackButton != null) BackButton.IsEnabled = ready && core!.CanGoBack;
            if (ForwardButton != null) ForwardButton.IsEnabled = ready && core!.CanGoForward;
            if (RefreshButton != null) RefreshButton.IsEnabled = ready;
            if (DevToolsButton != null) DevToolsButton.IsEnabled = ready;
        }

        private void UpdateNavigateUrl()
        {
            var source = _webView?.CoreWebView2?.Source;
            if (string.IsNullOrWhiteSpace(source) || NavigateUrl == null)
            {
                return;
            }

            if (source.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(NavigateUrl.Text, source, StringComparison.OrdinalIgnoreCase))
            {
                _suppressUrlTextChanged = true;
                NavigateUrl.Text = source;
                _suppressUrlTextChanged = false;
            }
        }

        private static string? NormalizeUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var trimmed = raw.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            {
                return absolute.ToString();
            }

            var withScheme = "https://" + trimmed;
            if (Uri.TryCreate(withScheme, UriKind.Absolute, out var normalized))
            {
                return normalized.ToString();
            }

            return trimmed;
        }

        private async Task ProxyToRunnerAsync(CapturedRequest item)
        {
            if (!ShouldProxy(item))
            {
                return;
            }
            await ExecuteRunnerAsync(item).ConfigureAwait(false);
        }

        private Task<ExecuteResponse> ExecuteRunnerAsync(CapturedRequest item)
        {
            var key = BuildProxyKey(item);
            lock (_proxyTasks)
            {
                if (_proxyTasks.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                var task = ExecuteRunnerInternalAsync(item, key);
                _proxyTasks[key] = task;
                return task;
            }
        }

        private async Task<ExecuteResponse> ExecuteRunnerInternalAsync(CapturedRequest item, string key)
        {
            try
            {
                if (BeforeAutoProxyAsync != null)
                {
                    await BeforeAutoProxyAsync(item).ConfigureAwait(false);
                }

                // Clone headers and inject impersonation if active
                var headers = item.HeadersDictionary != null
                    ? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(item.HeadersDictionary, StringComparer.OrdinalIgnoreCase)
                    : new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);

                InjectImpersonationHeader(headers);

                var execRequest = new ExecuteRequest
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    Request = new InterceptedHttpRequest
                    {
                        Method = item.Method,
                        Url = item.OriginalUrl ?? item.Url,
                        Body = item.Body ?? Array.Empty<byte>(),
                        Headers = headers
                    }
                };

                var response = await _runnerClient.ExecuteAsync(execRequest, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
                item.ResponseStatus = response.Response.StatusCode;
                item.ResponseBody = response.Response.Body;
                item.ResponseHeadersDictionary = response.Response.Headers ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
                item.ResponseBodyPreview = GetBodyPreview(response.Response.Body);
                item.ResponseTraceLines = response.Trace.TraceLines ?? new System.Collections.Generic.List<string>();
                AppendGlobalTrace(response.Trace?.TraceLines);
                if (item.ResponseStatus >= 400)
                {
                    LogService.Append($"Proxied {item.Method} {item.OriginalUrl} (status {item.ResponseStatus})");
                }

                if (AfterAutoProxyAsync != null)
                {
                    _ = AfterAutoProxyAsync(item, response);
                }
                return response;
            }
            finally
            {
                lock (_proxyTasks)
                {
                    _proxyTasks.Remove(key);
                }
            }
        }

        private CoreWebView2WebResourceResponse? BuildWebViewResponse(ExecuteResponse response)
        {
            try
            {
                var env = _webView?.CoreWebView2?.Environment;
                if (env == null)
                {
                    return null;
                }

                var body = response.Response.Body ?? Array.Empty<byte>();
                var stream = new MemoryStream(body);
                var status = response.Response.StatusCode;
                var statusText = status >= 200 && status < 300 ? "OK" : "Error";
                var headers = ToHeaderString(response.Response.Headers);
                return env.CreateWebResourceResponse(stream, status, statusText, headers);
            }
            catch
            {
                return null;
            }
        }

        private static string ToHeaderString(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? headers)
        {
            if (headers == null || headers.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var kvp in headers)
            {
                var values = kvp.Value;
                if (values == null || values.Count == 0)
                {
                    builder.Append(kvp.Key).Append(": ").Append("\r\n");
                    continue;
                }
                foreach (var value in values)
                {
                    builder.Append(kvp.Key).Append(": ").Append(value).Append("\r\n");
                }
            }
            return builder.ToString();
        }

        private static string GetBodyPreview(byte[] body)
        {
            if (body == null || body.Length == 0)
            {
                return "(empty)";
            }

            var len = Math.Min(body.Length, MaxBodyBytes);
            try
            {
                return System.Text.Encoding.UTF8.GetString(body, 0, len);
            }
            catch
            {
                return $"(binary data {body.Length} bytes)";
            }
        }

        private bool ShouldProxy(CapturedRequest item)
        {
            var key = BuildProxyKey(item);
            var now = DateTime.UtcNow;
            lock (_recentProxies)
            {
                var cutoff = now.AddMilliseconds(-ProxyDedupWindowMs);
                var remove = _recentProxies.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
                foreach (var r in remove) _recentProxies.Remove(r);

                if (_recentProxies.TryGetValue(key, out var ts) && ts >= cutoff)
                {
                    return false;
                }

                _recentProxies[key] = now;
                return true;
            }
        }

        private static string BuildProxyKey(CapturedRequest item)
        {
            var bodyHash = ComputeBodyHash(item.Body);
            return $"{item.Method}|{item.OriginalUrl}|{bodyHash}";
        }

        private static string? TryGetHeader(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers, string name)
        {
            if (headers.TryGetValue(name, out var list) && list != null && list.Count > 0)
            {
                return list[0];
            }
            return null;
        }


        private void OnSettingsChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressUrlTextChanged)
            {
                return;
            }
            if (NavigateUrl == null)
            {
                return;
            }
            _settings.NavigateUrl = NavigateUrl.Text;
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CaptureSettingsModel.CaptureEnabled))
            {
                CaptureToggle.IsChecked = _settings.CaptureEnabled;
            }

            if (e.PropertyName == nameof(CaptureSettingsModel.AutoProxy))
            {
                AutoProxyToggle.IsChecked = _settings.AutoProxy;
            }

            if (e.PropertyName == nameof(CaptureSettingsModel.AutoDebugMatched))
            {
                _suppressDebugToggle = true;
                DebugToggle.IsChecked = _settings.AutoDebugMatched;
                _suppressDebugToggle = false;
            }
        }

        private void OnBrowserSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BrowserSettingsModel.OpenDevToolsOnActivate))
            {
                return;
            }

            QueueApplyBrowserSettings();
        }

        public void RequestOpenDevToolsOnReady()
        {
            _pendingDevToolsOpen = true;
            TryOpenDevToolsIfReady();
        }

        private void TryOpenDevToolsIfReady()
        {
            if (!_pendingDevToolsOpen)
            {
                return;
            }

            if (!_webViewReady || _webView?.CoreWebView2 == null)
            {
                return;
            }

            _pendingDevToolsOpen = false;
            _webView.CoreWebView2.OpenDevToolsWindow();
        }

        public async Task ApplyBrowserSettingsAsync()
        {
            if (!_webViewReady || _webView?.CoreWebView2 == null)
            {
                return;
            }

            var core = _webView.CoreWebView2;
            try
            {
                await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            }
            catch
            {
                return;
            }

            await TrySetDevToolsBoolAsync(core, "Network.setCacheDisabled", "cacheDisabled", _browserSettings.DisableCaching);
            await TrySetDevToolsBoolAsync(core, "Network.setBypassServiceWorker", "bypass", _browserSettings.BypassServiceWorker);
        }

        private void QueueApplyBrowserSettings()
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => _ = ApplyBrowserSettingsAsync()), DispatcherPriority.Background);
        }

        private static async Task TrySetDevToolsBoolAsync(CoreWebView2 core, string method, string prop, bool value)
        {
            try
            {
                var json = $"{{\"{prop}\":{value.ToString().ToLowerInvariant()}}}";
                await core.CallDevToolsProtocolMethodAsync(method, json);
            }
            catch
            {
                // best effort
            }
        }

        private void OnCaptureToggleChanged(object sender, RoutedEventArgs e)
        {
            _settings.CaptureEnabled = CaptureToggle.IsChecked == true;
        }

        public void ApplyEnvironment(string? navigateUrl, bool apiOnly, bool autoProxy)
        {
            if (!string.IsNullOrWhiteSpace(navigateUrl))
            {
                _suppressUrlTextChanged = true;
                NavigateUrl.Text = navigateUrl;
                _suppressUrlTextChanged = false;
                _settings.NavigateUrl = navigateUrl;
                NavigateToDefault();
            }
            _settings.ApiOnly = apiOnly;
            _settings.AutoProxy = autoProxy;
            if (!string.IsNullOrWhiteSpace(navigateUrl) && _webViewReady)
            {
                _webView?.CoreWebView2?.Navigate(navigateUrl);
            }
        }

        public async Task ApplyEnvironmentAsync(string? navigateUrl, bool apiOnly, bool autoProxy, string? webViewCachePath)
        {
            _userDataFolder = webViewCachePath;
            var requiresRecreate = _webViewReady && !string.Equals(_userDataFolder, _currentUserDataFolder, StringComparison.OrdinalIgnoreCase);
            if (requiresRecreate || _webView == null)
            {
                CreateWebViewControl();
                _webViewReady = false;
            }
            await EnsureWebViewAsync();
            ApplyEnvironment(navigateUrl, apiOnly, autoProxy);
        }

        private void CreateWebViewControl()
        {
            if (_webView != null)
            {
                try
                {
                    if (_webView.CoreWebView2 != null)
                    {
                        _webView.CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                        _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                        _webView.CoreWebView2.ProcessFailed -= OnProcessFailed;
                        _webView.CoreWebView2.HistoryChanged -= OnHistoryChanged;
                        _webView.CoreWebView2.SourceChanged -= OnSourceChanged;
                    }
                    _webView.Dispose();
                }
                catch { }
            }

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            WebViewContainer.Children.Clear();
            WebViewContainer.Children.Add(_webView);
        }

        private void OnAutoProxyToggleChanged(object sender, RoutedEventArgs e)
        {
            _settings.AutoProxy = AutoProxyToggle.IsChecked == true;
        }

        private void OnDebugToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressDebugToggle) return;
            var enabled = DebugToggle.IsChecked == true;
            if (enabled)
            {
                var instances = VisualStudioAttachService.GetRunningInstances();
                if (instances.Count == 0)
                {
                    MessageBox.Show("No running Visual Studio instances found.", "Attach Debugger", MessageBoxButton.OK, MessageBoxImage.Information);
                    SetDebugToggle(false);
                    return;
                }

                var dialog = new DebuggerAttachDialog(instances)
                {
                    Owner = Window.GetWindow(this)
                };
                if (dialog.ShowDialog() != true || dialog.SelectedInstance == null)
                {
                    SetDebugToggle(false);
                    return;
                }

                if (!VisualStudioAttachService.AttachToRunner(dialog.SelectedInstance, out var error))
                {
                    MessageBox.Show($"Failed to attach debugger: {error}", "Attach Debugger", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetDebugToggle(false);
                    return;
                }

                _attachedDebugger = dialog.SelectedInstance;
                _settings.AutoDebugMatched = true;
                _debugMonitorTimer.Start();
                NotifyDebugStateChanged(true);
                return;
            }

            DisableDebugging(null);
        }

        private void SetDebugToggle(bool enabled)
        {
            _suppressDebugToggle = true;
            DebugToggle.IsChecked = enabled;
            _suppressDebugToggle = false;
            _settings.AutoDebugMatched = enabled;
            NotifyDebugStateChanged(enabled);
        }

        public bool IsDebuggingActive => _attachedDebugger != null && DebugToggle?.IsChecked == true;

        public void SetWebViewVisibility(bool visible)
        {
            if (WebViewContainer == null) return;
            WebViewContainer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        public void NotifyRunnerRestarted()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(NotifyRunnerRestarted);
                return;
            }

            if (DebugToggle?.IsChecked != true)
            {
                return;
            }

            DisableDebugging("Runner restarted or reloaded. Attach debugger again when ready.");
        }

        private void DisableDebugging(string? reason)
        {
            DisableDebuggingInternal(reason, true);
        }

        private void DisableDebuggingInternal(string? reason, bool detachRunner)
        {
            _settings.AutoDebugMatched = false;
            if (detachRunner && _attachedDebugger != null)
            {
                if (!VisualStudioAttachService.DetachRunner(_attachedDebugger, out var error) && !string.IsNullOrWhiteSpace(error))
                {
                    MessageBox.Show($"Failed to detach debugger: {error}", "Detach Debugger", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            _attachedDebugger = null;

            _suppressDebugToggle = true;
            DebugToggle.IsChecked = false;
            _suppressDebugToggle = false;

            if (_debugMonitorTimer.IsEnabled)
            {
                _debugMonitorTimer.Stop();
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                LogService.Append(reason);
            }

            NotifyDebugStateChanged(false);
        }

        public void RequestDebugToggle(bool enabled)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => RequestDebugToggle(enabled));
                return;
            }

            if (DebugToggle == null)
            {
                return;
            }

            var current = DebugToggle.IsChecked == true;
            if (current == enabled)
            {
                return;
            }

            DebugToggle.IsChecked = enabled;
        }

        private void NotifyDebugStateChanged(bool enabled)
        {
            DebugStateChanged?.Invoke(enabled);
        }

        private void OnDebugMonitorTick(object? sender, EventArgs e)
        {
            if (_attachedDebugger == null || DebugToggle?.IsChecked != true)
            {
                if (_debugMonitorTimer.IsEnabled)
                {
                    _debugMonitorTimer.Stop();
                }
                return;
            }

            if (!VisualStudioAttachService.IsRunnerAttached(_attachedDebugger))
            {
                DisableDebuggingInternal("Debugger detached in Visual Studio.", false);
            }
        }

        private void AppendGlobalTrace(System.Collections.Generic.IEnumerable<string>? lines)
        {
            if (lines == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AppendGlobalTrace(lines));
                return;
            }
            foreach (var line in lines)
            {
                _globalTrace.Add($"{DateTime.Now:HH:mm:ss} {line}");
                if (_globalTrace.Count > 200)
                {
                    _globalTrace.RemoveAt(0);
                }
            }
        }

        #region Form Tools

        /// <summary>
        /// Executes JavaScript in the WebView2 browser.
        /// </summary>
        /// <param name="script">The JavaScript code to execute.</param>
        /// <returns>The result of script execution.</returns>
        private async Task<string?> ExecuteScriptAsync(string script)
        {
            if (_webView?.CoreWebView2 == null || !_webViewReady)
            {
                LogService.Append("WebView not ready for script execution");
                return null;
            }

            try
            {
                var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                return result;
            }
            catch (Exception ex)
            {
                LogService.Append($"Script execution error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles the Show Logical Names tool click.
        /// Shows logical names for all fields on the current form.
        /// </summary>
        private async void OnShowLogicalNamesClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.ShowLogicalNamesScript);
            LogService.Append("Executed: Show Logical Names");
        }

        /// <summary>
        /// Handles the Clear Logical Names tool click.
        /// Removes the logical name labels from the form.
        /// </summary>
        private async void OnClearLogicalNamesClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.ClearLogicalNamesScript);
            LogService.Append("Executed: Clear Logical Names");
        }

        /// <summary>
        /// Handles the God Mode tool click.
        /// Unlocks all fields, shows hidden elements, and removes field requirements.
        /// </summary>
        private async void OnGodModeClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.GodModeScript);
            LogService.Append("Executed: God Mode");
        }

        /// <summary>
        /// Handles the Show Changed Fields tool click.
        /// Highlights fields that have unsaved changes.
        /// </summary>
        private async void OnShowChangedFieldsClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.ShowChangedFieldsScript);
            LogService.Append("Executed: Show Changed Fields");
        }

        /// <summary>
        /// Handles the Show Option Set Values tool click.
        /// Displays numeric values for option set fields.
        /// </summary>
        private async void OnShowOptionSetValuesClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.ShowOptionSetValuesScript);
            LogService.Append("Executed: Show Option Set Values");
        }

        /// <summary>
        /// Handles the Form Info tool click.
        /// Shows metadata about the current form (ID, entity, form type).
        /// </summary>
        private async void OnFormInfoClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.ShowFormInfoScript);
            LogService.Append("Executed: Form Info");
        }

        /// <summary>
        /// Handles the Copy Record ID tool click.
        /// Copies the current record's GUID to the clipboard.
        /// </summary>
        private async void OnCopyRecordIdClick(object sender, RoutedEventArgs e)
        {
            var result = await ExecuteScriptAsync(FormToolsService.CopyRecordIdScript);
            if (result != null && result != "null" && result != "\"\"")
            {
                // Result is JSON-encoded, strip quotes
                var id = result.Trim('"');
                if (!string.IsNullOrEmpty(id))
                {
                    Clipboard.SetText(id);
                    LogService.Append($"Copied Record ID: {id}");
                }
            }
        }

        /// <summary>
        /// Handles the Copy Record URL tool click.
        /// Copies the direct URL to the current record to the clipboard.
        /// </summary>
        private async void OnCopyRecordUrlClick(object sender, RoutedEventArgs e)
        {
            var result = await ExecuteScriptAsync(FormToolsService.CopyRecordUrlScript);
            if (result != null && result != "null" && result != "\"\"")
            {
                var url = result.Trim('"');
                if (!string.IsNullOrEmpty(url))
                {
                    Clipboard.SetText(url);
                    LogService.Append($"Copied Record URL: {url}");
                }
            }
        }

        /// <summary>
        /// Handles the Refresh Without Save tool click.
        /// Refreshes the form without prompting to save changes.
        /// </summary>
        private async void OnRefreshWithoutSaveClick(object sender, RoutedEventArgs e)
        {
            await ExecuteScriptAsync(FormToolsService.RefreshWithoutSaveScript);
            LogService.Append("Executed: Refresh Without Save");
        }

        /// <summary>
        /// Handles the Open Web API tool click.
        /// Opens the Web API endpoint for the current record in a new browser tab.
        /// </summary>
        private async void OnOpenWebApiClick(object sender, RoutedEventArgs e)
        {
            var result = await ExecuteScriptAsync(FormToolsService.OpenWebApiScript);
            LogService.Append("Executed: Open Web API");
        }

        #endregion

        #region Impersonation

        /// <summary>
        /// Sets the environment profile and access token for impersonation user search.
        /// </summary>
        /// <param name="profile">The environment profile with org URL.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        public void SetEnvironmentContext(EnvironmentProfile? profile, string? accessToken)
        {
            _currentProfile = profile;
            _currentAccessToken = accessToken;
        }

        /// <summary>
        /// Gets the currently impersonated user, or null if none.
        /// </summary>
        public DataverseUser? ImpersonatedUser => _impersonatedUser;

        /// <summary>
        /// Handles the Impersonation button click.
        /// Opens the user search dialog to select a user for impersonation.
        /// </summary>
        private void OnImpersonationClick(object sender, RoutedEventArgs e)
        {
            if (_currentProfile == null || string.IsNullOrWhiteSpace(_currentAccessToken))
            {
                MessageBox.Show(
                    "Please connect to an environment first before selecting a user to impersonate.",
                    "Impersonation",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new UserSearchDialog(_currentProfile, _currentAccessToken, _impersonatedUser)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true)
            {
                if (dialog.ImpersonationCleared)
                {
                    ClearImpersonation();
                }
                else if (dialog.SelectedUser != null)
                {
                    SetImpersonatedUser(dialog.SelectedUser);
                }
            }
        }

        /// <summary>
        /// Sets the impersonated user for Web API requests.
        /// </summary>
        /// <param name="user">The user to impersonate.</param>
        public void SetImpersonatedUser(DataverseUser? user)
        {
            _impersonatedUser = user;
            UpdateImpersonationUI();

            if (user != null)
            {
                LogService.Append($"Impersonation enabled: {user.DisplayText} ({user.Id})");
                _settings.ImpersonatedUserId = user.Id;
                _settings.ImpersonatedUserName = user.DisplayText;
            }
        }

        /// <summary>
        /// Clears the current impersonation.
        /// </summary>
        public void ClearImpersonation()
        {
            _impersonatedUser = null;
            UpdateImpersonationUI();
            LogService.Append("Impersonation cleared");
            _settings.ImpersonatedUserId = null;
            _settings.ImpersonatedUserName = null;
        }

        /// <summary>
        /// Updates the impersonation button UI to reflect current state.
        /// </summary>
        private void UpdateImpersonationUI()
        {
            if (ImpersonationButton == null || ImpersonationText == null)
            {
                return;
            }

            if (_impersonatedUser != null)
            {
                ImpersonationText.Text = "👤 " + TruncateText(_impersonatedUser.FullName, 16);
                ImpersonationButton.Tag = "Active";
                ImpersonationButton.ToolTip = $"Impersonating: {_impersonatedUser.DisplayText}\nClick to change or clear";
            }
            else
            {
                ImpersonationText.Text = "Impersonate";
                ImpersonationButton.Tag = null;
                ImpersonationButton.ToolTip = "Select a user to impersonate for Web API requests (MSCRMCallerID header)";
            }
        }

        /// <summary>
        /// Truncates text to the specified maximum length.
        /// </summary>
        /// <param name="text">The text to truncate.</param>
        /// <param name="maxLength">Maximum length before truncation.</param>
        /// <returns>Truncated text with ellipsis if needed.</returns>
        private static string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text ?? string.Empty;
            }
            return text.Substring(0, maxLength - 3) + "...";
        }

        /// <summary>
        /// Adds the MSCRMCallerID header to the request headers if impersonation is active.
        /// </summary>
        /// <param name="headers">The headers dictionary to modify.</param>
        private void InjectImpersonationHeader(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers)
        {
            if (_impersonatedUser == null || _impersonatedUser.Id == Guid.Empty)
            {
                return;
            }

            var headerKey = "MSCRMCallerID";
            var headerValue = _impersonatedUser.Id.ToString();

            if (headers.ContainsKey(headerKey))
            {
                headers[headerKey] = new System.Collections.Generic.List<string> { headerValue };
            }
            else
            {
                headers.Add(headerKey, new System.Collections.Generic.List<string> { headerValue });
            }
        }

        #endregion

    }
}
