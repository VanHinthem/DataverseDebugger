using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Runner;
using DataverseDebugger.App.Services;
using DataverseDebugger.Protocol;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View hosting the embedded DataverseRESTBuilder for crafting Web API requests.
    /// </summary>
    /// <remarks>
    /// Intercepts REST Builder requests and can proxy them through the local Runner
    /// for plugin debugging. Injects authentication tokens automatically.
    /// </remarks>
    public partial class RestBuilderView : UserControl
    {
        private static readonly string[] AllowedSchemes = { Uri.UriSchemeHttp, Uri.UriSchemeHttps };
        private static readonly int MaxItems = 500;
        private static readonly int MaxBodyBytes = 4096;
        private static readonly TimeSpan DataverseProxyTimeout = TimeSpan.FromMinutes(5);
        private static readonly System.Collections.Generic.HashSet<string> RequestHeaderBlocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "Host",
            "Connection",
            "Proxy-Connection",
            "Content-Length",
            "Accept-Encoding",
            "Cookie",
            "Authorization"
        };
        private static readonly System.Collections.Generic.HashSet<string> ResponseHeaderBlocklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "Transfer-Encoding",
            "Content-Encoding",
            "Content-Length",
            "Connection",
            "Keep-Alive"
        };
        private static readonly JsonSerializerOptions WebMessageJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private readonly RunnerClient _runnerClient;
        private readonly ObservableCollection<CapturedRequest> _requests;
        private readonly ObservableCollection<string> _globalTrace;
        private readonly CaptureSettingsModel _settings;
        private readonly BrowserSettingsModel _browserSettings;
        private readonly RestBuilderResourceHandler _resourceHandler = new RestBuilderResourceHandler();
        private readonly DrbXtbSettings _xtbSettings;
        private readonly HttpClient _dataverseClient = CreateDataverseHttpClient();
        private readonly System.Collections.Generic.Dictionary<string, Task<ExecuteResponse>> _proxyTasks = new(StringComparer.OrdinalIgnoreCase);

        private bool _webViewReady;
        private bool _initialized;
        private bool _pendingNavigate;
        private WebView2? _webView;
        private string? _userDataFolder;
        private string? _currentUserDataFolder;
        private EnvironmentProfile? _activeProfile;
        private bool _suppressDebugToggle;

        public Func<CapturedRequest, Task>? BeforeAutoProxyAsync { get; set; }
        public Func<CapturedRequest, ExecuteResponse, Task>? AfterAutoProxyAsync { get; set; }
        public Action<bool>? DebugToggleRequested { get; set; }

        public RestBuilderView(RunnerClient runnerClient, ObservableCollection<CapturedRequest> requests, ObservableCollection<string> globalTrace, CaptureSettingsModel settings, BrowserSettingsModel browserSettings)
        {
            _runnerClient = runnerClient;
            _requests = requests;
            _globalTrace = globalTrace;
            _settings = settings;
            _browserSettings = browserSettings;
            _xtbSettings = new DrbXtbSettings(() => _activeProfile);
            InitializeComponent();
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            _browserSettings.PropertyChanged += OnBrowserSettingsPropertyChanged;
            Loaded += OnLoaded;
            ThemeService.ThemeChanged += OnThemeChanged;
            CaptureToggle.IsChecked = _settings.CaptureEnabled;
            AutoProxyToggle.IsChecked = _settings.AutoProxy;
            _suppressDebugToggle = true;
            DebugToggle.IsChecked = _settings.AutoDebugMatched;
            _suppressDebugToggle = false;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            CreateWebViewControl();
            await EnsureWebViewAsync();
        }

        public async Task ApplyEnvironmentAsync(EnvironmentProfile profile)
        {
            _activeProfile = profile;
            _userDataFolder = BuildUserDataFolder(profile.WebViewCachePath);
            var requiresRecreate = _webViewReady && !string.Equals(_userDataFolder, _currentUserDataFolder, StringComparison.OrdinalIgnoreCase);
            var createdNewWebView = false;
            if (!_initialized)
            {
                CreateWebViewControl();
                _initialized = true;
                createdNewWebView = true;
            }
            else if (requiresRecreate || _webView == null)
            {
                CreateWebViewControl();
                createdNewWebView = true;
            }

            if (createdNewWebView)
            {
                _webViewReady = false;
            }

            _pendingNavigate = true;

            if (!IsLoaded)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            }

            await EnsureWebViewAsync();
        }

        public async Task<bool> InjectCapturedRequestAsync(RestBuilderInjectionRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (!IsLoaded)
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            }
            if (!_initialized)
            {
                CreateWebViewControl();
                _initialized = true;
            }

            await EnsureWebViewAsync();
            if (!_webViewReady || _webView?.CoreWebView2 == null)
            {
                return false;
            }

            try
            {
                var envelope = new { action = "captured-request", data = request };
                var json = JsonSerializer.Serialize(envelope, WebMessageJsonOptions);
                _webView.CoreWebView2.PostWebMessageAsJson(json);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Append($"[RestBuilder] Failed to post captured request: {ex.Message}");
                return false;
            }
        }

        private static string? BuildUserDataFolder(string? basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                return null;
            }

            return Path.Combine(basePath, "restbuilder");
        }

        private static HttpClient CreateDataverseHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            return new HttpClient(handler)
            {
                Timeout = DataverseProxyTimeout
            };
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
                    var core = _webView.CoreWebView2;
                    core.NavigationCompleted += OnNavigationCompleted;
                    core.ProcessFailed += OnProcessFailed;
                    core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                    core.WebResourceRequested += OnWebResourceRequested;
                    core.WebResourceResponseReceived += OnWebResourceResponseReceived;
                    TryAddHostObject(core);
                    _webViewReady = true;
                    _currentUserDataFolder = _userDataFolder;
                    UpdateNavButtons();
                    QueueApplyBrowserSettings();
                    QueueApplyTheme();
                }

                if (_pendingNavigate)
                {
                    _pendingNavigate = false;
                    NavigateToRestBuilder();
                }
            }
            catch
            {
                // best effort
            }
        }

        private void TryAddHostObject(CoreWebView2 core)
        {
            try
            {
                core.AddHostObjectToScript("xtbSettings", _xtbSettings);
            }
            catch
            {
                // ignore if already added
            }
        }

        public async Task EnsureInjectionHandlerReadyAsync(TimeSpan timeout)
        {
            if (!_webViewReady || _webView?.CoreWebView2 == null)
            {
                throw new InvalidOperationException("REST Builder WebView is not ready.");
            }

            var deadline = DateTime.UtcNow + timeout;
            var core = _webView.CoreWebView2;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var result = await core.ExecuteScriptAsync("typeof window.__drbReceiveCapturedRequest === 'function'");
                    if (!string.IsNullOrWhiteSpace(result) && result.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Append($"[RestBuilder] ExecuteScript check failed: {ex.Message}");
                }

                await Task.Delay(200);
            }

            throw new TimeoutException("REST Builder scripts are not ready yet.");
        }


        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateNavButtons();
            QueueApplyTheme();
        }

        private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
        {
            UpdateNavButtons();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            QueueApplyTheme();
        }

        private async void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (_webView?.CoreWebView2 == null)
            {
                return;
            }

            if (_resourceHandler.TryHandle(e, _webView.CoreWebView2.Environment))
            {
                return;
            }

            if (!_settings.CaptureEnabled)
            {
                return;
            }

            var method = e.Request.Method;
            if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
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
                if (ShouldIntercept(method))
                {
                    item.AutoProxied = true;
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
                }
                else if (TryResolveAuthorization(rawHeadersMap, out var authHeader))
                {
                    item.AutoProxied = true;
                    var deferral = e.GetDeferral();
                    try
                    {
                        var response = await ProxyToDataverseAsync(item, e.Request, authHeader!).ConfigureAwait(true);
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
                }
            }
        }

        private void OnWebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            try
            {
                var req = e.Request;
                var method = req.Method;
                var url = req.Uri;
                var status = e.Response.StatusCode;

                var match = _requests
                    .Where(r => string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(r.OriginalUrl, url, StringComparison.OrdinalIgnoreCase) &&
                                (!r.ResponseStatus.HasValue || r.ResponseStatus == 0))
                    .LastOrDefault();

                if (match != null)
                {
                    match.ResponseStatus = status;
                    Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
            }
            catch
            {
                // ignore
            }
        }

        private static (string Flattened, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> RawMap) FlattenHeaders(CoreWebView2HttpRequestHeaders headers)
        {
            try
            {
                var lines = new StringBuilder();
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
                    Query = string.Empty
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
            return false;
        }

        private static string ComputeBodyHash(byte[]? body)
        {
            if (body == null || body.Length == 0) return "empty";
            using var sha = SHA256.Create();
            var len = Math.Min(body.Length, 4096);
            var hash = sha.ComputeHash(body, 0, len);
            return Convert.ToHexString(hash);
        }

        private static bool IsSafeForAutoProxy(string method)
        {
            return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldIntercept(string method)
        {
            return !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
        }

        private bool TryResolveAuthorization(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers, out string? authHeader)
        {
            authHeader = TryGetHeader(headers, "Authorization");
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                return true;
            }

            var token = _activeProfile?.LastAccessToken;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var expiresOn = _activeProfile?.AccessTokenExpiresOn;
            if (expiresOn.HasValue && expiresOn.Value <= DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return false;
            }

            authHeader = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? token
                : $"Bearer {token}";
            return true;
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

                var execRequest = new ExecuteRequest
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    Request = new InterceptedHttpRequest
                    {
                        Method = item.Method,
                        Url = item.OriginalUrl ?? item.Url,
                        Body = item.Body ?? Array.Empty<byte>(),
                        Headers = item.HeadersDictionary
                    }
                };

                var response = await _runnerClient.ExecuteAsync(execRequest, DataverseProxyTimeout).ConfigureAwait(false);
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

        private sealed class ProxyResponse
        {
            public int StatusCode { get; }
            public byte[] Body { get; }
            public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> Headers { get; }

            public ProxyResponse(int statusCode, byte[] body, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers)
            {
                StatusCode = statusCode;
                Body = body;
                Headers = headers;
            }
        }

        private async Task<ProxyResponse> ProxyToDataverseAsync(CapturedRequest item, CoreWebView2WebResourceRequest request, string authHeader)
        {
            try
            {
                using var message = new HttpRequestMessage(new HttpMethod(item.Method), request.Uri);
                if (MethodAllowsBody(item.Method) && item.Body != null && item.Body.Length > 0)
                {
                    message.Content = new ByteArrayContent(item.Body);
                }

                CopyRequestHeaders(item.HeadersDictionary, message);
                if (!string.IsNullOrWhiteSpace(authHeader))
                {
                    message.Headers.TryAddWithoutValidation("Authorization", authHeader);
                }

                using var response = await _dataverseClient.SendAsync(message).ConfigureAwait(false);
                var body = response.Content != null
                    ? await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
                    : Array.Empty<byte>();
                var headers = FlattenResponseHeaders(response);
                var status = (int)response.StatusCode;

                UpdateCapturedResponse(item, status, body, headers);
                if (status >= 400)
                {
                    LogService.Append($"Proxied {item.Method} {item.OriginalUrl} (status {status})");
                }

                return new ProxyResponse(status, body, headers);
            }
            catch (OperationCanceledException)
            {
                return BuildErrorResponse(item, 504, "Dataverse request timed out.");
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(item, 502, $"Dataverse proxy failed: {ex.Message}");
            }
        }

        private ProxyResponse BuildErrorResponse(CapturedRequest item, int statusCode, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            var headers = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Content-Type", new System.Collections.Generic.List<string> { "text/plain; charset=utf-8" } }
            };

            UpdateCapturedResponse(item, statusCode, body, headers);
            LogService.Append($"{message} ({item.Method} {item.OriginalUrl})");
            return new ProxyResponse(statusCode, body, headers);
        }

        private static void CopyRequestHeaders(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> source, HttpRequestMessage target)
        {
            foreach (var kvp in source)
            {
                if (RequestHeaderBlocklist.Contains(kvp.Key))
                {
                    continue;
                }

                if (!target.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value))
                {
                    if (target.Content == null)
                    {
                        continue;
                    }

                    target.Content.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
                }
            }
        }

        private static bool MethodAllowsBody(string method)
        {
            return !string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
        }

        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> FlattenResponseHeaders(HttpResponseMessage response)
        {
            var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
            {
                AddHeader(map, header.Key, header.Value);
            }
            foreach (var header in response.Content.Headers)
            {
                AddHeader(map, header.Key, header.Value);
            }
            return map;
        }

        private static void AddHeader(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> map, string name, System.Collections.Generic.IEnumerable<string> values)
        {
            if (!map.TryGetValue(name, out var list))
            {
                list = new System.Collections.Generic.List<string>();
                map[name] = list;
            }

            foreach (var value in values)
            {
                list.Add(value);
            }
        }

        private void UpdateCapturedResponse(CapturedRequest item, int status, byte[] body, System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers)
        {
            item.ResponseStatus = status;
            item.ResponseBody = body;
            item.ResponseHeadersDictionary = headers ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            item.ResponseBodyPreview = GetBodyPreview(body);
            Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
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

        private CoreWebView2WebResourceResponse? BuildWebViewResponse(ProxyResponse response)
        {
            try
            {
                var env = _webView?.CoreWebView2?.Environment;
                if (env == null)
                {
                    return null;
                }

                var body = response.Body ?? Array.Empty<byte>();
                var stream = new MemoryStream(body);
                var status = response.StatusCode;
                var statusText = status >= 200 && status < 300 ? "OK" : "Error";
                var headers = ToHeaderString(SanitizeResponseHeaders(response.Headers));
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

        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> SanitizeResponseHeaders(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers)
        {
            var sanitized = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in headers)
            {
                if (ResponseHeaderBlocklist.Contains(kvp.Key))
                {
                    continue;
                }

                sanitized[kvp.Key] = kvp.Value;
            }

            return sanitized;
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

        private void OnCaptureToggleChanged(object sender, RoutedEventArgs e)
        {
            _settings.CaptureEnabled = CaptureToggle.IsChecked == true;
        }

        private void OnAutoProxyToggleChanged(object sender, RoutedEventArgs e)
        {
            _settings.AutoProxy = AutoProxyToggle.IsChecked == true;
        }

        private void OnDebugToggleChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressDebugToggle)
            {
                return;
            }

            DebugToggleRequested?.Invoke(DebugToggle.IsChecked == true);
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

        public void SetWebViewVisibility(bool visible)
        {
            if (WebViewContainer == null)
            {
                return;
            }

            WebViewContainer.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            try
            {
                _resourceHandler.ReloadResources();
            }
            catch (Exception ex)
            {
                LogService.Append($"[RestBuilder] Reload failed: {ex.Message}");
            }

            NavigateToRestBuilder();
        }

        private void OnDevToolsClick(object sender, RoutedEventArgs e)
        {
            _webView?.CoreWebView2?.OpenDevToolsWindow();
        }

        private void UpdateNavButtons()
        {
            var core = _webView?.CoreWebView2;
            var ready = core != null;
            if (RefreshButton != null) RefreshButton.IsEnabled = ready;
            if (DevToolsButton != null) DevToolsButton.IsEnabled = ready;
        }

        private void NavigateToRestBuilder()
        {
            var url = GetRestBuilderUrl();
            if (!string.IsNullOrWhiteSpace(url))
            {
                _webView?.CoreWebView2?.Navigate(url);
            }
        }

        private string? GetRestBuilderUrl()
        {
            var baseUrl = NormalizeOrgUrl(_activeProfile?.OrgUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            return $"{baseUrl.TrimEnd('/')}/WebResources/{RestBuilderResourceHandler.FakeIdentifier}/gp_/drb/drb_index.htm";
        }

        private static string? NormalizeOrgUrl(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var trimmed = raw.Trim();
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) &&
                (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
            {
                return absolute.GetLeftPart(UriPartial.Authority);
            }

            var withScheme = "https://" + trimmed;
            if (Uri.TryCreate(withScheme, UriKind.Absolute, out var normalized))
            {
                return normalized.GetLeftPart(UriPartial.Authority);
            }

            return trimmed;
        }

        private async Task ApplyBrowserSettingsAsync()
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

        private void QueueApplyTheme()
        {
            if (!_webViewReady || _webView?.CoreWebView2 == null)
            {
                return;
            }

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() => _ = ApplyThemeToWebViewAsync()), DispatcherPriority.Background);
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

        private async Task ApplyThemeToWebViewAsync()
        {
            var core = _webView?.CoreWebView2;
            if (core == null)
            {
                return;
            }

            var theme = ThemeService.IsDarkMode ? "dark" : "light";
            var script = $"window.__drbApplyTheme && window.__drbApplyTheme('{theme}')";
            try
            {
                await core.ExecuteScriptAsync(script).ConfigureAwait(false);
            }
            catch
            {
                // best effort
            }
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
                        _webView.CoreWebView2.WebResourceResponseReceived -= OnWebResourceResponseReceived;
                        _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
                        _webView.CoreWebView2.ProcessFailed -= OnProcessFailed;
                    }
                    _webView.Dispose();
                }
                catch
                {
                    // ignore
                }
            }

            _webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            WebViewContainer.Children.Clear();
            WebViewContainer.Children.Add(_webView);
        }

        private void AppendGlobalTrace(System.Collections.Generic.IEnumerable<string>? lines)
        {
            if (lines == null)
            {
                return;
            }

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
    }
}
