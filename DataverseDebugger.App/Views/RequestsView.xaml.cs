using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Runner;
using DataverseDebugger.Protocol;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View for displaying and managing captured HTTP requests.
    /// </summary>
    /// <remarks>
    /// Shows captured requests, matches them to registered plugin steps,
    /// and provides controls for executing plugins locally with debugging.
    /// </remarks>
    public partial class RequestsView : UserControl
    {
        private static readonly int MaxBodyBytes = 4096;
        private const int MaxBuilderRequestNameLength = 80;
        private static readonly System.Collections.Generic.Dictionary<string, string> BuilderRequestTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Retrieve", "retrievesingle" },
            { "RetrieveMultiple", "retrievemultiple" },
            { "Create", "create" },
            { "Update", "update" },
            { "Delete", "delete" },
            { "Associate", "associate" },
            { "Disassociate", "disassociate" }
        };

        public event Action<bool>? PluginDebuggingChanged;
        public event Func<RestBuilderInjectionRequest, Task>? SendToBuilderRequested;

        public ObservableCollection<CapturedRequest> Requests { get; }
        public ObservableCollection<string> GlobalRunnerTrace { get; }
        public ObservableCollection<StepInfoModel> CatalogSteps { get; } = new();
        public ObservableCollection<ExecutionTreeNodeItem> ExecutionTreeItems { get; } = new();

        private readonly RunnerClient _runnerClient;
        private readonly CaptureSettingsModel _settings;
        private readonly RunnerSettingsModel _runnerSettings;
        private readonly Action<StepInfoModel>? _stepSetter;
        private PluginCatalog? _catalog;
        private System.Collections.Generic.Dictionary<string, string> _entityMap = new(System.StringComparer.OrdinalIgnoreCase);
        private System.Collections.Generic.Dictionary<string, string> _logicalToEntitySet = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.Dictionary<string, OperationParameterSource> _operationSources = new(System.StringComparer.OrdinalIgnoreCase);
        private System.Collections.Generic.Dictionary<string, string> _assemblyPaths = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly System.Collections.Generic.HashSet<string> _selectedAssemblyNames = new(System.StringComparer.OrdinalIgnoreCase);
        private readonly WebApiRequestParser _requestParser = new WebApiRequestParser();
        private System.Collections.Generic.List<StepInfoModel> _matchedSteps = new();
        private System.Collections.Generic.List<StepInfoModel> _candidateSteps = new();
        private StepInfoModel? _selectedTreeStep;
        private readonly System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.List<PluginImageItem>> _imagesByStepId = new();
        private readonly System.Collections.Generic.HashSet<CapturedRequest> _autoDebuggedSync = new();
        private readonly System.Collections.Generic.HashSet<CapturedRequest> _autoDebuggedAsync = new();
        private int _pluginDebuggingCount;
        private EnvironmentProfile? _activeProfile;
        private string? _activeToken;
        private string? _metadataPath;
        private readonly ICollectionView? _requestsView;
        private bool _showMatchIndicator = true;
        private bool _showConvertibleIndicator;
        private bool _showStepIndicator = true;
        private bool _showNoIndicator;

        public RequestsView(ObservableCollection<CapturedRequest> requests, RunnerClient runnerClient, CaptureSettingsModel settings, RunnerSettingsModel runnerSettings, ObservableCollection<string>? globalTrace = null, Action<StepInfoModel>? stepSetter = null)
        {
            Requests = requests;
            _runnerClient = runnerClient;
            _settings = settings;
            _runnerSettings = runnerSettings;
            GlobalRunnerTrace = globalTrace ?? new ObservableCollection<string>();
            _stepSetter = stepSetter;
            _requestsView = CollectionViewSource.GetDefaultView(Requests);
            if (_requestsView != null)
            {
                _requestsView.Filter = FilterRequest;
            }
            InitializeComponent();
            DataContext = this;
            _settings.PropertyChanged += OnSettingsPropertyChanged;
            Loaded += OnLoaded;
            Requests.CollectionChanged += OnRequestsCollectionChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // no-op
        }
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RequestList.SelectedItem is CapturedRequest item)
            {
                RequestInfoText.Text = $"Method: {item.Method}{Environment.NewLine}Url: {item.OriginalUrl}{Environment.NewLine}Time: {item.Timestamp:HH:mm:ss}";
                HeadersText.Text = item.Headers;
                BodyText.Text = item.BodyPreview;
                RunnerResponseText.Text = item.ResponseStatus.HasValue ? $"Runner response: {item.ResponseStatus}" : "Runner response: -";
                RunnerResponseBodyText.Text = item.ResponseBodyPreview ?? "-";
                TryMapToStep(item);
            }
        }

        private void OnRequestListScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (RequestHeaderTransform == null) return;
            RequestHeaderTransform.X = -e.HorizontalOffset;
        }

        private void OnClearRequests(object sender, RoutedEventArgs e)
        {
            Requests.Clear();
            _autoDebuggedSync.Clear();
            _autoDebuggedAsync.Clear();
            RequestInfoText.Text = "-";
            HeadersText.Text = "-";
            BodyText.Text = "-";
            RunnerResponseText.Text = "Runner response: -";
            RunnerResponseBodyText.Text = "-";
            _selectedTreeStep = null;
            ExecutionTreeItems.Clear();
            _matchedSteps.Clear();
            _candidateSteps.Clear();
            GlobalRunnerTrace.Clear();
            UpdateDebugButtonState();
            RefreshRequestFilter();
        }

        private void OnRequestFilterToggleChanged(object sender, RoutedEventArgs e)
        {
            _showMatchIndicator = MatchFilterToggle?.IsChecked != false;
            _showStepIndicator = StepFilterToggle?.IsChecked != false;
            _showConvertibleIndicator = ConvertibleFilterToggle?.IsChecked == true;
            _showNoIndicator = NoIndicatorFilterToggle?.IsChecked == true;
            RefreshRequestFilter();
        }

        private async void OnSendToRunner(object sender, RoutedEventArgs e)
        {
            if (RequestList.SelectedItem is not CapturedRequest item)
            {
                RunnerResponseText.Text = "Runner response: select a request first.";
                return;
            }

            await ProxyToRunnerAsync(item);
        }

        private async void OnSendToBuilder(object sender, RoutedEventArgs e)
        {
            if (RequestList.SelectedItem is not CapturedRequest item)
            {
                MessageBox.Show("Select a request first.", "REST Builder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!TryBuildRestBuilderRequest(item, out var builderRequest, out var error))
            {
                var message = string.IsNullOrWhiteSpace(error)
                    ? "Unable to send this request to the builder."
                    : error;
                MessageBox.Show(message, "REST Builder", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (SendToBuilderRequested == null)
            {
                MessageBox.Show("REST Builder is not available right now.", "REST Builder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await SendToBuilderRequested.Invoke(builderRequest);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send request to builder: {ex.Message}", "REST Builder", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnDebugMatchedStep(object sender, RoutedEventArgs e)
        {
            if (RequestList.SelectedItem is not CapturedRequest item)
            {
                RunnerResponseText.Text = "Runner response: select a request first.";
                return;
            }

            var step = _selectedTreeStep;
            if (step == null)
            {
                RunnerResponseText.Text = "Runner response: select a matched step from the execution tree first.";
                return;
            }

            await PrepareImagesBeforeProxyAsync(item, new[] { step });
            await PrepareImagesAfterResponseAsync(item, null, new[] { step });
            await RunDebugStepsAsync(item, new[] { step }, updateUi: true);
        }

        private async Task RunDebugStepsAsync(CapturedRequest item, System.Collections.Generic.IEnumerable<StepInfoModel> steps, bool updateUi)
        {
            var plan = steps
                .Where(s => s != null)
                .OrderBy(s => StageOrder(s.Stage))
                .ThenBy(s => s.Mode)
                .ThenBy(s => s.Rank)
                .ToList();

            if (plan.Count == 0)
            {
                if (updateUi)
                {
                    RunnerResponseText.Text = "Runner response: no matched steps.";
                }
                return;
            }

            SetPluginDebuggingActive(true);
            if (updateUi)
            {
                RunnerResponseText.Text = $"Runner response: invoking {plan.Count} steps...";
                RunnerResponseBodyText.Text = "(plugin invoke)";
            }

            var aggregate = new System.Collections.Generic.List<string>();
            try
            {
                for (var i = 0; i < plan.Count; i++)
                {
                    var step = plan[i];
                    if (updateUi)
                    {
                        RunnerResponseText.Text = $"Runner response: step {i + 1}/{plan.Count} {step.TypeName} (Stage {step.Stage}, Mode {step.Mode})";
                    }

                    var response = await ExecutePluginDebugAsync(item, step, aggregate);
                    if (response?.TraceLines != null && response.TraceLines.Count > 0)
                    {
                        aggregate.AddRange(response.TraceLines);
                    }
                }
            }
            finally
            {
                SetPluginDebuggingActive(false);
            }

            item.ResponseTraceLines = aggregate;
            if (updateUi)
            {
                // trace panel removed
            }
            AppendGlobalTrace(aggregate);
            if (updateUi)
            {
                RunnerResponseText.Text = $"Plugin debug: {plan.Count} steps executed.";
            }
        }

        private void SetPluginDebuggingActive(bool active)
        {
            var next = active
                ? Interlocked.Increment(ref _pluginDebuggingCount)
                : Interlocked.Decrement(ref _pluginDebuggingCount);

            if (next < 0)
            {
                Interlocked.Exchange(ref _pluginDebuggingCount, 0);
                next = 0;
            }

            if (active && next == 1)
            {
                PluginDebuggingChanged?.Invoke(true);
            }
            else if (!active && next == 0)
            {
                PluginDebuggingChanged?.Invoke(false);
            }
        }

        private async Task<PluginInvokeResponse?> ExecutePluginDebugAsync(CapturedRequest item, StepInfoModel step, System.Collections.Generic.List<string> aggregate)
        {
            var assemblyPath = ResolveAssemblyPath(step.Assembly);
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                aggregate.Add($"Assembly not found for step: {step.Assembly}");
                return null;
            }

            var inferredMessage = InferMessage(item.Method, item.Url ?? item.OriginalUrl ?? string.Empty, out var inferredEntity);
            var messageName = string.IsNullOrWhiteSpace(step.MessageName) ? inferredMessage : step.MessageName;
            var primaryEntity = string.IsNullOrWhiteSpace(step.PrimaryEntity) ? inferredEntity : step.PrimaryEntity;
            var entityId = ExtractEntityId(item.OriginalUrl ?? item.Url ?? string.Empty);
            var targetJson = BuildTargetJson(item, primaryEntity, entityId);

            aggregate.Add($"--- {step.TypeName} [{messageName} {primaryEntity} Stage {step.Stage}, Mode {step.Mode}] ---");
            var request = new PluginInvokeRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Assembly = assemblyPath,
                TypeName = step.TypeName,
                MessageName = string.IsNullOrWhiteSpace(messageName) ? "Create" : messageName,
                PrimaryEntityName = string.IsNullOrWhiteSpace(primaryEntity) ? "entity" : primaryEntity,
                PrimaryEntityId = entityId ?? string.Empty,
                Stage = step.Stage,
                Mode = step.Mode,
                OrgUrl = _activeProfile?.OrgUrl,
                AccessToken = _activeToken,
                WriteMode = _runnerSettings?.WriteMode,
                UnsecureConfiguration = step.UnsecureConfiguration,
                SecureConfiguration = step.SecureConfiguration,
                HttpRequest = new InterceptedHttpRequest
                {
                    Method = item.Method,
                    Url = item.OriginalUrl ?? item.Url ?? string.Empty,
                    Headers = item.HeadersDictionary ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase),
                    Body = item.Body ?? Array.Empty<byte>(),
                    CorrelationId = item.ClientRequestId
                },
                TargetJson = targetJson
            };
            if (item.StepImages.TryGetValue(step.StepId, out var stepImages) && stepImages.Count > 0)
            {
                request.Images = stepImages;
            }

            var response = await _runnerClient.ExecutePluginAsync(request, TimeSpan.FromSeconds(30));
            return response;
        }

        private static int StageOrder(int stage)
        {
            return stage switch
            {
                10 => 1,
                20 => 2,
                30 => 3,
                40 => 4,
                _ => 5
            };
        }

        public async Task ProxyToRunnerAsync(CapturedRequest item)
        {
            RunnerResponseText.Text = "Runner response: sending...";
            var execRequest = new ExecuteRequest
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Request = new InterceptedHttpRequest
                {
                    Method = item.Method,
                    Url = item.OriginalUrl ?? item.Url,
                    Body = item.Body ?? Array.Empty<byte>(),
                    Headers = item.HeadersDictionary
                },
                ForceProxy = false,
                BypassAuthPreflight = false
            };

            var response = await _runnerClient.ExecuteAsync(execRequest, TimeSpan.FromSeconds(30), line =>
            {
                Dispatcher.Invoke(() =>
                {
                    item.ResponseTraceLines.Add(line);
                });
            });
            RunnerResponseText.Text = $"Runner response: {response.Response.StatusCode} - {string.Join(" | ", response.Trace.TraceLines)}";
            item.ResponseHeadersDictionary = response.Response.Headers ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            item.ResponseBody = response.Response.Body;
            RunnerResponseBodyText.Text = GetBodyPreview(response.Response.Body);
            item.ResponseStatus = response.Response.StatusCode;
            item.ResponseBodyPreview = GetBodyPreview(response.Response.Body);
            item.ResponseTraceLines = response.Trace.TraceLines ?? new System.Collections.Generic.List<string>();
            AppendGlobalTrace(response.Trace?.TraceLines);
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
                GlobalRunnerTrace.Add($"{DateTime.Now:HH:mm:ss} {line}");
                if (GlobalRunnerTrace.Count > 200)
                {
                    GlobalRunnerTrace.RemoveAt(0);
                }
            }
        }

        private void TryMapToStep(CapturedRequest req)
        {
            if (req == null) return;

            var matches = GetMatches(req, log: true);
            var candidates = GetCandidateSteps(req, log: false);
            UpdateMatchesList(matches, candidates);
            UpdateExecutionTree(req);
        }

        public async Task HandleAutoDebugBeforeProxyAsync(CapturedRequest req)
        {
            if (!_settings.AutoDebugMatched || req == null)
            {
                return;
            }

            var matches = GetMatches(req, log: false)
                .Where(s => IsAssemblySelected(s.Assembly))
                .ToList();

            var preSteps = matches.Where(s => s.Stage < 30).ToList();
            if (preSteps.Count == 0 || _autoDebuggedSync.Contains(req))
            {
                return;
            }

            await PrepareImagesBeforeProxyAsync(req, preSteps);
            _autoDebuggedSync.Add(req);
            await RunDebugStepsAsync(req, preSteps, updateUi: false);
        }

        public async Task HandleAutoDebugAfterResponseAsync(CapturedRequest req, ExecuteResponse response)
        {
            if (!_settings.AutoDebugMatched || req == null)
            {
                return;
            }

            var matches = GetMatches(req, log: false)
                .Where(s => IsAssemblySelected(s.Assembly))
                .ToList();

            var postSteps = matches.Where(s => s.Stage >= 30).ToList();
            if (postSteps.Count == 0 || _autoDebuggedAsync.Contains(req))
            {
                return;
            }

            await PrepareImagesAfterResponseAsync(req, response, postSteps);
            _autoDebuggedAsync.Add(req);
            await RunDebugStepsAsync(req, postSteps, updateUi: false);
        }

        private Task PrepareImagesBeforeProxyAsync(CapturedRequest req, System.Collections.Generic.IEnumerable<StepInfoModel>? steps = null)
            => PrepareImagesForStepsAsync(req, steps, includePre: true, includePost: false, response: null);

        private Task PrepareImagesAfterResponseAsync(CapturedRequest req, ExecuteResponse? response, System.Collections.Generic.IEnumerable<StepInfoModel>? steps = null)
            => PrepareImagesForStepsAsync(req, steps, includePre: false, includePost: true, response: response);

        private async Task PrepareImagesForStepsAsync(CapturedRequest req, System.Collections.Generic.IEnumerable<StepInfoModel>? steps, bool includePre, bool includePost, ExecuteResponse? response)
        {
            if (_activeProfile == null || string.IsNullOrWhiteSpace(_activeToken) || req == null)
            {
                return;
            }

            var stepList = steps?.Where(s => s != null).ToList()
                ?? GetMatches(req, log: false).Where(s => IsAssemblySelected(s.Assembly)).ToList();
            if (stepList.Count == 0)
            {
                return;
            }

            if (!TryResolveEntityContext(req, out var logicalName, out var entitySetName))
            {
                return;
            }

            var url = req.OriginalUrl ?? req.Url ?? string.Empty;
            var urlEntityId = TryParseEntityIdFromUrl(url);
            Guid? responseEntityId = null;
            if (includePost)
            {
                responseEntityId = TryGetEntityIdFromResponse(response, req, logicalName);
            }

            var preCache = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var postCache = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in stepList)
            {
                var stepImages = GetStepImages(step.StepId).ToList();
                if (stepImages.Count == 0)
                {
                    continue;
                }

                var stepLogical = string.IsNullOrWhiteSpace(step.PrimaryEntity) ? logicalName : step.PrimaryEntity;
                if (string.IsNullOrWhiteSpace(stepLogical))
                {
                    continue;
                }

                var stepEntitySet = ResolveEntitySetName(stepLogical, entitySetName);
                if (string.IsNullOrWhiteSpace(stepEntitySet))
                {
                    continue;
                }

                if (includePre && urlEntityId.HasValue && urlEntityId.Value != Guid.Empty)
                {
                    foreach (var image in stepImages.Where(IsPreImage))
                    {
                        var attributes = ParseAttributeList(image.Attributes);
                        var selectAttributes = ResolveLookupSelectAttributes(stepLogical, attributes);
                        var cacheKey = BuildImageCacheKey(stepEntitySet, urlEntityId.Value, selectAttributes);
                        if (!preCache.TryGetValue(cacheKey, out var entityJson))
                        {
                            entityJson = await PluginImageFetchService.FetchEntityJsonAsync(
                                _activeProfile,
                                _activeToken,
                                stepEntitySet,
                                stepLogical,
                                urlEntityId.Value,
                                selectAttributes.Count == 0 ? null : selectAttributes).ConfigureAwait(false);
                            preCache[cacheKey] = entityJson;
                        }

                        if (!string.IsNullOrWhiteSpace(entityJson))
                        {
                            var alias = string.IsNullOrWhiteSpace(image.EntityAlias) ? "PreImage" : image.EntityAlias;
                            UpsertImagePayload(req, step.StepId, new PluginImagePayload
                            {
                                ImageType = "PreImage",
                                EntityAlias = alias,
                                EntityJson = entityJson
                            });
                        }
                    }
                }

                if (includePost && !string.Equals(req.Method, "DELETE", StringComparison.OrdinalIgnoreCase))
                {
                    var postId = responseEntityId ?? urlEntityId;
                    if (postId.HasValue && postId.Value != Guid.Empty)
                    {
                        foreach (var image in stepImages.Where(IsPostImage))
                        {
                            var attributes = ParseAttributeList(image.Attributes);
                            var selectAttributes = ResolveLookupSelectAttributes(stepLogical, attributes);
                            var cacheKey = BuildImageCacheKey(stepEntitySet, postId.Value, selectAttributes);
                            if (!postCache.TryGetValue(cacheKey, out var entityJson))
                            {
                                entityJson = await PluginImageFetchService.FetchEntityJsonAsync(
                                    _activeProfile,
                                    _activeToken,
                                    stepEntitySet,
                                    stepLogical,
                                    postId.Value,
                                    selectAttributes.Count == 0 ? null : selectAttributes).ConfigureAwait(false);
                                postCache[cacheKey] = entityJson;
                            }

                            if (!string.IsNullOrWhiteSpace(entityJson))
                            {
                                var alias = string.IsNullOrWhiteSpace(image.EntityAlias) ? "PostImage" : image.EntityAlias;
                                UpsertImagePayload(req, step.StepId, new PluginImagePayload
                                {
                                    ImageType = "PostImage",
                                    EntityAlias = alias,
                                    EntityJson = entityJson
                                });
                            }
                        }
                    }
                }
            }
        }

        private static bool IsPreImage(PluginImageItem image)
            => image.ImageType.Equals("PreImage", StringComparison.OrdinalIgnoreCase)
                || image.ImageType.Equals("Both", StringComparison.OrdinalIgnoreCase);

        private static bool IsPostImage(PluginImageItem image)
            => image.ImageType.Equals("PostImage", StringComparison.OrdinalIgnoreCase)
                || image.ImageType.Equals("Both", StringComparison.OrdinalIgnoreCase);

        private System.Collections.Generic.IEnumerable<PluginImageItem> GetStepImages(Guid stepId)
            => _imagesByStepId.TryGetValue(stepId, out var list) ? list : System.Linq.Enumerable.Empty<PluginImageItem>();

        private static System.Collections.Generic.List<string> ParseAttributeList(string? attributes)
        {
            if (string.IsNullOrWhiteSpace(attributes))
            {
                return new System.Collections.Generic.List<string>();
            }

            return attributes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildImageDisplayText(PluginImageItem image)
        {
            var alias = string.IsNullOrWhiteSpace(image.EntityAlias) ? "-" : image.EntityAlias;
            var imageType = string.IsNullOrWhiteSpace(image.ImageType) ? "Image" : image.ImageType;
            var attrSummary = BuildImageAttributeSummary(image);
            return $"{imageType} Alias={alias} Attrs={attrSummary}";
        }

        private static string BuildImageTreeTitle(PluginImageItem image)
        {
            var alias = string.IsNullOrWhiteSpace(image.EntityAlias) ? "-" : image.EntityAlias;
            var imageType = string.IsNullOrWhiteSpace(image.ImageType) ? "Image" : image.ImageType;
            return $"{imageType} Alias={alias}";
        }

        private static string BuildImageTooltip(PluginImageItem image)
        {
            var alias = string.IsNullOrWhiteSpace(image.EntityAlias) ? "-" : image.EntityAlias;
            var imageType = string.IsNullOrWhiteSpace(image.ImageType) ? "Image" : image.ImageType;
            return $"{imageType} alias '{alias}'";
        }

        private static string BuildImageAttributeSummary(PluginImageItem image)
        {
            var attrs = ParseAttributeList(image.Attributes);
            return attrs.Count == 0 ? "(all)" : string.Join(", ", attrs);
        }

        private static int GetImageOrder(string? imageType)
        {
            if (string.IsNullOrWhiteSpace(imageType))
            {
                return 3;
            }

            if (imageType.Equals("PreImage", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (imageType.Equals("PostImage", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (imageType.Equals("Both", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 3;
        }

        private System.Collections.Generic.List<string> ResolveLookupSelectAttributes(string logicalName, System.Collections.Generic.List<string> attributes)
        {
            if (attributes == null || attributes.Count == 0)
            {
                return attributes ?? new System.Collections.Generic.List<string>();
            }

            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return attributes;
            }

            var lookups = MetadataCacheService.LoadLookupAttributes(_metadataPath, logicalName);
            if (lookups.Count == 0)
            {
                return attributes;
            }

            var resolved = new System.Collections.Generic.List<string>();
            foreach (var attribute in attributes)
            {
                if (string.IsNullOrWhiteSpace(attribute))
                {
                    continue;
                }

                if (attribute.IndexOf('@') >= 0)
                {
                    resolved.Add(attribute);
                    continue;
                }

                if (attribute.StartsWith("_", StringComparison.OrdinalIgnoreCase) &&
                    attribute.EndsWith("_value", StringComparison.OrdinalIgnoreCase))
                {
                    resolved.Add(attribute);
                    continue;
                }

                if (lookups.Contains(attribute))
                {
                    resolved.Add($"_{attribute}_value");
                }
                else
                {
                    resolved.Add(attribute);
                }
            }

            return resolved.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string BuildImageCacheKey(string entitySet, Guid id, System.Collections.Generic.List<string> attributes)
        {
            if (attributes == null || attributes.Count == 0)
            {
                return $"{entitySet}|{id}|*";
            }

            return $"{entitySet}|{id}|{string.Join(",", attributes)}";
        }

        private bool TryResolveEntityContext(CapturedRequest req, out string logicalName, out string entitySetName)
        {
            logicalName = string.Empty;
            entitySetName = string.Empty;

            var url = req.Url ?? req.OriginalUrl ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            if (_requestParser.TryParse(req.Method, url, out var parsed, out _))
            {
                logicalName = parsed.PrimaryEntity ?? string.Empty;
                entitySetName = parsed.EntitySetName ?? ExtractEntitySet(url);
            }
            else
            {
                InferMessage(req.Method, url, out logicalName);
                entitySetName = ExtractEntitySet(url);
            }

            if (string.IsNullOrWhiteSpace(logicalName) && !string.IsNullOrWhiteSpace(entitySetName))
            {
                if (_entityMap.TryGetValue(entitySetName, out var mappedLogical))
                {
                    logicalName = mappedLogical;
                }
            }

            if (string.IsNullOrWhiteSpace(entitySetName) && !string.IsNullOrWhiteSpace(logicalName))
            {
                if (_logicalToEntitySet.TryGetValue(logicalName, out var mappedSet))
                {
                    entitySetName = mappedSet;
                }
            }

            return !string.IsNullOrWhiteSpace(logicalName) || !string.IsNullOrWhiteSpace(entitySetName);
        }

#pragma warning disable CS8601
        private bool TryBuildRestBuilderRequest(CapturedRequest req, out RestBuilderInjectionRequest payload, out string? error)
        {
            payload = new RestBuilderInjectionRequest();
            error = null;

            var rawUrl = req.OriginalUrl ?? req.Url ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawUrl) || rawUrl.IndexOf("/api/data/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = "Only Dataverse Web API requests can be sent to the builder.";
                return false;
            }

            var parsed = TryParseWebApiRequest(req);
            if (!TryMapRequestType(req, parsed, out var requestType))
            {
                error = "This request type is not supported in the builder yet.";
                return false;
            }

            if (!TryResolveEntityContext(req, out var logicalName, out var entitySetName) || string.IsNullOrWhiteSpace(logicalName))
            {
                error = "Unable to determine the target entity for this request.";
                return false;
            }

            var headers = req.HeadersDictionary ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            var bodyText = TryDecodeBodyForBuilder(req.Body, out var isBinaryBody, out var bodyBase64);
            var flattenedHeaders = FlattenHeaders(headers);
            var queryParameters = ParseQueryParameters(rawUrl);

            var builderRequestType = requestType;
            string? builderQueryType = null;
            string? builderFetchXml = null;
            var dataverseOperationName = ExtractDataverseOperationName(rawUrl);

            if (string.Equals(builderRequestType, "retrievemultiple", StringComparison.OrdinalIgnoreCase)
                && queryParameters.TryGetValue("fetchXml", out var fetchXmlValue)
                && !string.IsNullOrWhiteSpace(fetchXmlValue))
            {
                builderRequestType = "predefinedquery";
                builderQueryType = "fetchxml";
                builderFetchXml = fetchXmlValue;
            }
#pragma warning disable CS8601
            payload = new RestBuilderInjectionRequest
            {
                RequestName = BuildRequestName(req, logicalName),
                RequestType = builderRequestType,
                Method = req.Method ?? string.Empty,
                Url = rawUrl,
                PrimaryEntityLogicalName = logicalName,
                EntitySetName = entitySetName,
                PrimaryId = TryParseEntityIdFromUrl(rawUrl)?.ToString(),
                DataverseOperationName = dataverseOperationName,
                Headers = flattenedHeaders,
                ContentType = TryGetContentType(headers),
                Query = queryParameters,
                QueryType = builderQueryType,
                FetchXml = builderFetchXml,
                Body = bodyText,
                BodyIsBinary = isBinaryBody,
                BodyBase64 = bodyBase64,
                Timestamp = req.Timestamp
            };
#pragma warning restore CS8601
            return true;
        }

        private ParsedWebApiRequest? TryParseWebApiRequest(CapturedRequest req)
#pragma warning restore CS8601
        {
            var url = req.OriginalUrl ?? req.Url ?? string.Empty;
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            return _requestParser.TryParse(req.Method ?? string.Empty, url, out var parsed, out _)
                ? parsed
                : null;
        }

        private bool TryMapRequestType(CapturedRequest req, ParsedWebApiRequest? parsed, out string requestType)
        {
            requestType = string.Empty;
            if (parsed != null)
            {
                if (BuilderRequestTypes.TryGetValue(parsed.MessageName, out var mappedByMessage) && mappedByMessage != null)
                {
                    requestType = mappedByMessage;
                    return true;
                }

                foreach (var candidate in parsed.MessageCandidates)
                {
                    if (BuilderRequestTypes.TryGetValue(candidate, out var mappedByCandidate) && mappedByCandidate != null)
                    {
                        requestType = mappedByCandidate;
                        return true;
                    }
                }
            }

            if (TryMapRequestTypeFromOperation(req, out requestType))
            {
                return true;
            }

            return TryMapRequestTypeFromHttpMethod(req, out requestType);
        }

        private static bool TryMapRequestTypeFromHttpMethod(CapturedRequest req, out string requestType)
        {
            requestType = string.Empty;
            var method = req.Method ?? string.Empty;
            var url = req.OriginalUrl ?? req.Url ?? string.Empty;
            if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var hasKey = url.IndexOf('(') >= 0 && url.IndexOf(')') > url.IndexOf('(');
                requestType = hasKey ? "retrievesingle" : "retrievemultiple";
                return true;
            }

            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            {
                requestType = "create";
                return true;
            }

            if (string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase) || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase))
            {
                requestType = "update";
                return true;
            }

            if (string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase))
            {
                requestType = "delete";
                return true;
            }

            return false;
        }

        private bool TryMapRequestTypeFromOperation(CapturedRequest req, out string requestType)
        {
            requestType = string.Empty;
            if (!TryExtractDataverseOperationName(req.OriginalUrl ?? req.Url ?? string.Empty, out var operationName))
            {
                return false;
            }

            if (TryResolveOperationSource(operationName, out var source))
            {
                requestType = source == OperationParameterSource.CustomApi ? "executecustomapi" : "executecustomaction";
                return true;
            }

            var method = req.Method ?? string.Empty;
            var isFunction = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
            var isCustom = IsCustomDataverseOperation(operationName);
            requestType = isFunction
                ? (isCustom ? "executecustomapi" : "executefunction")
                : (isCustom ? "executecustomaction" : "executeaction");
            return true;
        }

        private static bool TryExtractDataverseOperationName(string? url, out string operationName)
        {
            operationName = ExtractDataverseOperationName(url) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(operationName);
        }

        private static string? ExtractDataverseOperationName(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            const string marker = "Microsoft.Dynamics.CRM.";
            var index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return null;
            }

            var start = index + marker.Length;
            if (start >= url.Length)
            {
                return null;
            }

            var end = start;
            while (end < url.Length)
            {
                var ch = url[end];
                if (ch == '(' || ch == '?' || ch == '/' || ch == '&')
                {
                    break;
                }
                end++;
            }

            if (end <= start)
            {
                return null;
            }

            var segment = url.Substring(start, end - start);
            return string.IsNullOrWhiteSpace(segment) ? null : segment;
        }

        private static bool IsCustomDataverseOperation(string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                return false;
            }

            return operationName.IndexOf('_') > 0;
        }

        private bool TryResolveOperationSource(string? operationName, out OperationParameterSource source)
        {
            if (!string.IsNullOrWhiteSpace(operationName) && _operationSources.TryGetValue(operationName, out source))
            {
                return true;
            }

            source = default;
            return false;
        }

        private static Dictionary<string, string> ParseQueryParameters(string url)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var uri = TryCreateUri(url);
            if (uri == null || string.IsNullOrWhiteSpace(uri.Query))
            {
                return result;
            }

            var trimmed = uri.Query.TrimStart('?');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return result;
            }

            var pairs = trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kvp = pair.Split('=', 2);
                var key = Uri.UnescapeDataString(kvp[0]);
                var value = kvp.Length > 1 ? Uri.UnescapeDataString(kvp[1]) : string.Empty;
                if (!result.ContainsKey(key))
                {
                    result[key] = value;
                }
            }

            return result;
        }

        private static Uri? TryCreateUri(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            {
                return absolute;
            }

            if (!url.StartsWith("/", StringComparison.Ordinal))
            {
                url = "/" + url;
            }

            return Uri.TryCreate("https://placeholder" + url, UriKind.Absolute, out var fallback)
                ? fallback
                : null;
        }

        private static string BuildRequestName(CapturedRequest req, string? entityName)
        {
            var target = entityName;
            if (string.IsNullOrWhiteSpace(target))
            {
                target = req.Url ?? req.OriginalUrl ?? "request";
            }

            var baseName = $"{(req.Method ?? string.Empty).ToUpperInvariant()} {target}".Trim();
            if (baseName.Length <= MaxBuilderRequestNameLength)
            {
                return baseName;
            }

            return baseName.Substring(0, MaxBuilderRequestNameLength - 3) + "...";
        }

        private static Dictionary<string, string> FlattenHeaders(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? headers)
        {
            var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null)
            {
                return flattened;
            }

            foreach (var kvp in headers)
            {
                if (kvp.Value == null || kvp.Value.Count == 0)
                {
                    continue;
                }

                flattened[kvp.Key] = string.Join("; ", kvp.Value);
            }

            return flattened;
        }

        private static string? TryDecodeBodyForBuilder(byte[]? body, out bool isBinary, out string? base64)
        {
            isBinary = false;
            base64 = null;
            if (body == null || body.Length == 0)
            {
                return null;
            }

            var len = Math.Min(body.Length, MaxBodyBytes);
            try
            {
                return Encoding.UTF8.GetString(body, 0, len);
            }
            catch
            {
                isBinary = true;
                base64 = Convert.ToBase64String(body);
                return null;
            }
        }

        private static string? TryGetContentType(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>? headers)
        {
            if (headers == null)
            {
                return null;
            }

            return headers.TryGetValue("Content-Type", out var values) && values != null && values.Count > 0
                ? values[0]
                : null;
        }

        private string ResolveEntitySetName(string logicalName, string fallbackEntitySet)
        {
            if (!string.IsNullOrWhiteSpace(logicalName) && _logicalToEntitySet.TryGetValue(logicalName, out var setName))
            {
                return setName;
            }

            return fallbackEntitySet;
        }

        private static Guid? TryParseEntityIdFromUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var raw = ExtractEntityId(url);
            if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            var tokens = url.Split(new[] { '(', ')', '/', '\\', '{', '}', '?', '&', '=' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var trimmed = token.Trim('\'');
                if (Guid.TryParse(trimmed, out parsed))
                {
                    return parsed;
                }
            }

            return null;
        }

        private static Guid? TryGetEntityIdFromResponse(ExecuteResponse? response, CapturedRequest req, string? logicalName)
        {
            var headers = response?.Response.Headers ?? req.ResponseHeadersDictionary;
            if (headers != null && headers.Count > 0)
            {
                var headerId = TryGetEntityIdFromHeaders(headers);
                if (headerId.HasValue) return headerId;
            }

            var body = response?.Response.Body ?? req.ResponseBody;
            return TryParseEntityIdFromBody(body, logicalName);
        }

        private static Guid? TryGetEntityIdFromHeaders(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers)
        {
            if (headers == null || headers.Count == 0)
            {
                return null;
            }

            if (TryGetHeaderValue(headers, "OData-EntityId", out var entityIdValue))
            {
                var id = TryParseEntityIdFromUrl(entityIdValue);
                if (id.HasValue) return id;
            }

            if (TryGetHeaderValue(headers, "Location", out var locationValue))
            {
                var id = TryParseEntityIdFromUrl(locationValue);
                if (id.HasValue) return id;
            }

            return null;
        }

        private static Guid? TryParseEntityIdFromBody(byte[]? body, string? logicalName)
        {
            if (body == null || body.Length == 0)
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                if (doc.RootElement.TryGetProperty("@odata.id", out var odataId) && odataId.ValueKind == JsonValueKind.String)
                {
                    var id = TryParseEntityIdFromUrl(odataId.GetString());
                    if (id.HasValue) return id;
                }

                if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                {
                    if (Guid.TryParse(idProp.GetString(), out var id)) return id;
                }

                if (!string.IsNullOrWhiteSpace(logicalName))
                {
                    var logicalId = logicalName + "id";
                    if (doc.RootElement.TryGetProperty(logicalId, out var logicalIdProp) && logicalIdProp.ValueKind == JsonValueKind.String)
                    {
                        if (Guid.TryParse(logicalIdProp.GetString(), out var id)) return id;
                    }
                }

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (!prop.Name.EndsWith("id", StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.String && Guid.TryParse(prop.Value.GetString(), out var id))
                    {
                        return id;
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }

            return null;
        }

        private static bool TryGetHeaderValue(System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> headers, string name, out string? value)
        {
            value = null;
            if (headers.TryGetValue(name, out var list) && list != null && list.Count > 0)
            {
                value = list[0];
                return true;
            }

            return false;
        }

        private static void UpsertImagePayload(CapturedRequest req, Guid stepId, PluginImagePayload payload)
        {
            if (req == null || payload == null || string.IsNullOrWhiteSpace(payload.EntityJson))
            {
                return;
            }

            if (!req.StepImages.TryGetValue(stepId, out var list))
            {
                list = new System.Collections.Generic.List<PluginImagePayload>();
                req.StepImages[stepId] = list;
            }

            var existing = list.FirstOrDefault(p =>
                string.Equals(p.ImageType, payload.ImageType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.EntityAlias, payload.EntityAlias, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.EntityJson = payload.EntityJson;
            }
            else
            {
                list.Add(payload);
            }
        }

        private System.Collections.Generic.List<StepInfoModel> GetMatches(CapturedRequest req, bool log)
        {
            var results = new System.Collections.Generic.List<StepInfoModel>();
            if (req == null || CatalogSteps.Count == 0) return results;

            var url = req.Url ?? req.OriginalUrl ?? string.Empty;
            var messageNames = new System.Collections.Generic.List<string>();
            var messageForStage = string.Empty;
            var logicalName = string.Empty;
            var entitySet = string.Empty;

            var parsedOk = _requestParser.TryParse(req.Method, url, out var parsed, out var parseError);
            if (parsedOk)
            {
                messageNames = parsed.MessageCandidates;
                messageForStage = parsed.MessageName;
                logicalName = parsed.PrimaryEntity ?? string.Empty;
                entitySet = parsed.EntitySetName ?? ExtractEntitySet(url);
            }
            else
            {
                messageForStage = InferMessage(req.Method, url, out logicalName);
                if (!string.IsNullOrWhiteSpace(messageForStage))
                {
                    messageNames.Add(messageForStage);
                }
                entitySet = ExtractEntitySet(url);
                if (log && !string.IsNullOrWhiteSpace(parseError))
                {
                    LogService.Append($"Match parse failed: {parseError}");
                }
            }

            if (messageNames.Count == 0)
            {
                if (log) LogService.Append($"Match skipped: could not infer message for {req.Method} {url}");
                return results;
            }

            if (_entityMap.TryGetValue(entitySet, out var mappedLogical) && !string.IsNullOrWhiteSpace(mappedLogical) && string.IsNullOrWhiteSpace(logicalName))
            {
                logicalName = mappedLogical;
            }
            var entityCandidates = new[]
            {
                logicalName,
                entitySet,
                Singularize(entitySet)
            }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var bodyAttrs = ExtractBodyAttributes(req);
            var desiredStage = (messageForStage == "Retrieve" || messageForStage == "RetrieveMultiple") ? 30 : 0; // special case retrieve

            var messageCandidateSet = new System.Collections.Generic.HashSet<string>(messageNames, StringComparer.OrdinalIgnoreCase);

            var stepCandidates = CatalogSteps
                .Where(s => messageCandidateSet.Contains(s.MessageName))
                .Where(s =>
                {
                    if (string.IsNullOrWhiteSpace(s.PrimaryEntity))
                    {
                        return entityCandidates.Count == 0;
                    }
                    return entityCandidates.Any(c => string.Equals(c, s.PrimaryEntity, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            var messageMatches = CatalogSteps
                .Where(s => messageCandidateSet.Contains(s.MessageName))
                .ToList();
            if (messageMatches.Count == 0)
            {
                if (log) LogService.Append($"Match attempt: msg=[{string.Join(",", messageNames)}], entities=[{string.Join(",", entityCandidates)}], bodyAttrs={string.Join(",", bodyAttrs)}, candidates=0");
                return results;
            }

            var entityMatches = CatalogSteps
                .Where(s => messageCandidateSet.Contains(s.MessageName))
                .Where(s => FilteringMatches(bodyAttrs, s.FilteringAttributes))
                .Where(s =>
                {
                    if (string.IsNullOrWhiteSpace(s.PrimaryEntity)) return false;
                    return entityCandidates.Any(c => string.Equals(c, s.PrimaryEntity, StringComparison.OrdinalIgnoreCase));
                })
                .Select(s => new
                {
                    Step = s,
                    StageScore = ComputeStageScore(messageForStage, s.Stage, desiredStage),
                    ModeScore = s.Mode == 0 ? 1 : 0,
                    RankScore = s.Rank == 0 ? 0 : s.Rank
                })
                .OrderByDescending(x => x.StageScore)
                .ThenByDescending(x => x.ModeScore)
                .ThenBy(x => x.RankScore)
                .Select(x => x.Step)
                .ToList();

            var messageOnly = CatalogSteps
                .Where(s => messageCandidateSet.Contains(s.MessageName))
                .Where(s => FilteringMatches(bodyAttrs, s.FilteringAttributes))
                .Where(s =>
                {
                    if (string.IsNullOrWhiteSpace(s.PrimaryEntity)) return false; // only allow if entity known
                    return entityCandidates.Any(c => string.Equals(c, s.PrimaryEntity, StringComparison.OrdinalIgnoreCase));
                })
                .Select(s => new
                {
                    Step = s,
                    StageScore = ComputeStageScore(messageForStage, s.Stage, desiredStage),
                    ModeScore = s.Mode == 0 ? 1 : 0,
                    RankScore = s.Rank == 0 ? 0 : s.Rank
                })
                .OrderByDescending(x => x.StageScore)
                .ThenByDescending(x => x.ModeScore)
                .ThenBy(x => x.RankScore)
                .Select(x => x.Step)
                .ToList();

            results = entityMatches.Count > 0
                ? entityMatches
                : (messageOnly.Count > 0 ? messageOnly : new System.Collections.Generic.List<StepInfoModel>());

            if (log)
            {
                LogService.Append($"Match attempt: msg=[{string.Join(",", messageNames)}], entities=[{string.Join(",", entityCandidates)}], bodyAttrs={string.Join(",", bodyAttrs)}, entityMatches={entityMatches.Count}, msgOnly={messageOnly.Count}, candidates={messageMatches.Count}");
                if (entityMatches.Count == 0 && messageOnly.Count > 0)
                {
                    var first = messageOnly.First();
                    LogService.Append($"Match fallback using message-only: first={first.TypeName} / {first.PrimaryEntity} (Stage {first.Stage}, Mode {first.Mode})");
                }
                else if (entityMatches.Count == 0 && messageOnly.Count == 0 && results.Count == 0)
                {
                    LogService.Append("Match fallback: none found.");
                }
            }

            return results;
        }

        private System.Collections.Generic.List<StepInfoModel> GetCandidateSteps(CapturedRequest req, bool log)
        {
            var results = new System.Collections.Generic.List<StepInfoModel>();
            if (req == null || CatalogSteps.Count == 0) return results;

            var url = req.Url ?? req.OriginalUrl ?? string.Empty;
            var messageNames = new System.Collections.Generic.List<string>();
            var logicalName = string.Empty;
            var entitySet = string.Empty;

            var parsedOk = _requestParser.TryParse(req.Method, url, out var parsed, out var parseError);
            if (parsedOk)
            {
                messageNames = parsed.MessageCandidates;
                logicalName = parsed.PrimaryEntity ?? string.Empty;
                entitySet = parsed.EntitySetName ?? ExtractEntitySet(url);
            }
            else
            {
                var messageForStage = InferMessage(req.Method, url, out logicalName);
                if (!string.IsNullOrWhiteSpace(messageForStage))
                {
                    messageNames.Add(messageForStage);
                }
                entitySet = ExtractEntitySet(url);
                if (log && !string.IsNullOrWhiteSpace(parseError))
                {
                    LogService.Append($"Match parse failed: {parseError}");
                }
            }

            if (messageNames.Count == 0)
            {
                return results;
            }

            if (_entityMap.TryGetValue(entitySet, out var mappedLogical) && !string.IsNullOrWhiteSpace(mappedLogical) && string.IsNullOrWhiteSpace(logicalName))
            {
                logicalName = mappedLogical;
            }
            var entityCandidates = new[]
            {
                logicalName,
                entitySet,
                Singularize(entitySet)
            }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var messageCandidateSet = new System.Collections.Generic.HashSet<string>(messageNames, StringComparer.OrdinalIgnoreCase);

            results = CatalogSteps
                .Where(s => messageCandidateSet.Contains(s.MessageName))
                .Where(s =>
                {
                    if (string.IsNullOrWhiteSpace(s.PrimaryEntity))
                    {
                        return entityCandidates.Count == 0;
                    }
                    return entityCandidates.Any(c => string.Equals(c, s.PrimaryEntity, StringComparison.OrdinalIgnoreCase));
                })
                .ToList();

            return results;
        }

        private static string InferMessage(string method, string url, out string logicalName)
        {
            logicalName = string.Empty;
            var msg = method?.ToUpperInvariant() ?? string.Empty;

            logicalName = Singularize(ExtractEntitySet(url));

            if (msg == "POST") return "Create";
            if (msg == "PATCH" || msg == "MERGE") return "Update";
            if (msg == "DELETE") return "Delete";
            if (msg == "PUT") return "Update";
            if (msg == "GET")
            {
                return url.Contains("(") ? "Retrieve" : "RetrieveMultiple";
            }

            return string.Empty;
        }

        private static string? ExtractEntityId(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var open = path.IndexOf('(');
                if (open < 0) return null;
                var close = path.IndexOf(')', open + 1);
                if (close <= open) return null;
                var raw = path.Substring(open + 1, close - open - 1).Trim('\'');
                if (Guid.TryParse(raw, out var guid))
                {
                    return guid.ToString();
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        private static string ExtractEntitySet(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var apiIndex = path.IndexOf("/api/data/", StringComparison.OrdinalIgnoreCase);
                if (apiIndex >= 0)
                {
                    var after = path.Substring(apiIndex + "/api/data/".Length);
                    var segments = after.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length > 0)
                    {
                        // If the first segment is a version (e.g. v9.0), skip it
                        if (segments[0].StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                            segments[0].Length > 1 &&
                            char.IsDigit(segments[0][1]))
                        {
                            segments = segments.Skip(1).ToArray();
                        }
                    }
                    if (segments.Length > 0)
                    {
                        var first = segments[0];
                        var entitySet = first.Contains("(") ? first.Split('(')[0] : first;
                        return entitySet;
                    }
                }
            }
            catch
            {
                // ignore
            }
            return string.Empty;
        }

        private static string? BuildTargetJson(CapturedRequest item, string primaryEntity, string? entityId)
        {
            if (string.IsNullOrWhiteSpace(primaryEntity) && (item.Body == null || item.Body.Length == 0))
            {
                return null;
            }

            var raw = item.Body != null && item.Body.Length > 0
                ? System.Text.Encoding.UTF8.GetString(item.Body)
                : string.Empty;

            var hasLogicalName = false;
            var hasId = false;
            var inner = string.Empty;
            var hasBodyObject = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        hasBodyObject = true;
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.NameEquals("logicalName"))
                            {
                                hasLogicalName = true;
                            }
                            if (prop.NameEquals("id") || prop.NameEquals("Id"))
                            {
                                hasId = true;
                            }
                        }
                        inner = raw.Trim();
                        if (inner.StartsWith("{", StringComparison.Ordinal) && inner.EndsWith("}", StringComparison.Ordinal))
                        {
                            inner = inner.Substring(1, inner.Length - 2).Trim();
                        }
                    }
                }
                catch
                {
                    // ignore parse errors
                }
            }

            var sb = new StringBuilder();
            sb.Append("{");
            var wrote = false;
            if (!string.IsNullOrWhiteSpace(primaryEntity) && !hasLogicalName)
            {
                sb.Append("\"logicalName\":\"").Append(EscapeJson(primaryEntity)).Append("\"");
                wrote = true;
            }
            if (!string.IsNullOrWhiteSpace(entityId) && !hasId)
            {
                if (wrote) sb.Append(",");
                sb.Append("\"id\":\"").Append(EscapeJson(entityId)).Append("\"");
                wrote = true;
            }
            if (hasBodyObject && !string.IsNullOrWhiteSpace(inner))
            {
                if (wrote) sb.Append(",");
                sb.Append(inner);
            }
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Singularize(string entitySet)
        {
            if (string.IsNullOrWhiteSpace(entitySet)) return string.Empty;
            if (entitySet.EndsWith("ies", StringComparison.OrdinalIgnoreCase))
            {
                return entitySet.Substring(0, entitySet.Length - 3) + "y";
            }
            if (entitySet.EndsWith("sses", StringComparison.OrdinalIgnoreCase))
            {
                return entitySet.Substring(0, entitySet.Length - 2);
            }
            if (entitySet.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                return entitySet.Substring(0, entitySet.Length - 1);
            }
            return entitySet;
        }

        private static System.Collections.Generic.HashSet<string> ExtractBodyAttributes(CapturedRequest req)
        {
            var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (req?.Body == null || req.Body.Length == 0) return set;
            try
            {
                using var doc = JsonDocument.Parse(req.Body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        set.Add(prop.Name);
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }
            return set;
        }

        private static bool FilteringMatches(System.Collections.Generic.HashSet<string> bodyAttributes, string filteringAttributesCsv)
        {
            if (string.IsNullOrWhiteSpace(filteringAttributesCsv))
            {
                return true;
            }
            if (bodyAttributes == null || bodyAttributes.Count == 0)
            {
                // If the step specifies filtering attributes but the request has none, do not match
                return false;
            }

            var parts = filteringAttributesCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToList();
            if (parts.Count == 0) return true;

            return parts.Any(attr => bodyAttributes.Contains(attr));
        }

        private static int ComputeStageScore(string message, int stage, int desiredStage)
        {
            // For CRUD prefer PreOperation (20), then PreValidation (10), then Post (40)
            if (message == "Retrieve" || message == "RetrieveMultiple")
            {
                return stage == 30 ? 3 : (stage == 20 ? 2 : (stage == 10 ? 1 : 0));
            }

            if (stage == 20) return 3;
            if (stage == 10) return 2;
            if (stage == 40) return 1;
            if (desiredStage != 0 && stage == desiredStage) return 1;
            return 0;
        }

        private int CountSelectableTypes(System.Collections.Generic.List<StepInfoModel> matches)
        {
            if (matches == null || matches.Count == 0) return 0;
            return matches
                .Where(m => !string.IsNullOrWhiteSpace(m.TypeName))
                .Where(m => IsAssemblySelected(m.Assembly))
                .Select(m => m.TypeName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private void UpdateMatchesList(System.Collections.Generic.List<StepInfoModel> matches, System.Collections.Generic.List<StepInfoModel> candidates)
        {
            _matchedSteps = matches ?? new System.Collections.Generic.List<StepInfoModel>();
            _candidateSteps = candidates ?? new System.Collections.Generic.List<StepInfoModel>();

            var matchedTypeCount = CountSelectableTypes(_matchedSteps);

            if (RequestList.SelectedItem is CapturedRequest currentReq)
            {
                currentReq.MatchingTypesCount = matchedTypeCount;
                currentReq.HasSteps = _candidateSteps.Count > 0;
                RequestList.Items.Refresh();
                RefreshRequestFilter();
            }

            if (_selectedTreeStep != null && !_matchedSteps.Any(s => s.StepId == _selectedTreeStep.StepId))
            {
                _selectedTreeStep = null;
            }

            UpdateDebugButtonState();
        }

        private void UpdateDebugButtonState()
        {
            if (DebugStepButton == null)
            {
                return;
            }

            DebugStepButton.IsEnabled = _selectedTreeStep != null && IsAssemblySelected(_selectedTreeStep.Assembly);
        }

        private bool FilterRequest(object? item)
        {
            if (item is not CapturedRequest req)
            {
                return true;
            }

            var hasAnyIndicator = req.HasMatch || req.CanConvert || req.HasSteps;
            var includeMatch = _showMatchIndicator && req.HasMatch;
            var includeConvertible = _showConvertibleIndicator && req.CanConvert;
            var includeSteps = _showStepIndicator && req.HasSteps;
            var includeNone = _showNoIndicator && !hasAnyIndicator;

            return includeMatch || includeConvertible || includeSteps || includeNone;
        }

        private void RefreshRequestFilter()
        {
            _requestsView?.Refresh();
        }

        private void UpdateExecutionTree(CapturedRequest? request)
        {
            ExecutionTreeItems.Clear();
            if (ExecutionTreeSummary != null)
            {
                ExecutionTreeSummary.Text = "Matches: 0, Candidates: 0";
            }
            _selectedTreeStep = null;
            UpdateDebugButtonState();
            if (request == null)
            {
                return;
            }

            var rootTitle = $"{request.Method} {request.Url}";
            var root = new ExecutionTreeNodeItem(rootTitle)
            {
                ToolTip = $"{request.OriginalUrl}\nCaptured: {request.Timestamp:HH:mm:ss}",
                IsExpanded = true
            };
            ExecutionTreeItems.Add(root);

            var steps = _matchedSteps.Count > 0 ? _matchedSteps : _candidateSteps;
            if (ExecutionTreeSummary != null)
            {
                var matchCount = _matchedSteps.Count;
                var candidateCount = _candidateSteps.Count;
                ExecutionTreeSummary.Text = $"Matches: {matchCount}, Candidates: {candidateCount}";
            }
            if (steps.Count == 0)
            {
                root.Children.Add(new ExecutionTreeNodeItem("No candidate steps found.")
                {
                    IsSelectable = false
                });
                return;
            }

            root.Children.Add(new ExecutionTreeNodeItem("Estimated from catalog matches.")
            {
                IsSelectable = false,
                IsExpanded = true
            });

            var matchedIds = new System.Collections.Generic.HashSet<Guid>(_matchedSteps.Select(s => s.StepId));
            var stageGroups = steps
                .GroupBy(s => s.Stage)
                .OrderBy(g => StageOrder(g.Key));

            foreach (var group in stageGroups)
            {
                var stageNode = new ExecutionTreeNodeItem(StageLabel(group.Key))
                {
                    IsSelectable = false,
                    IsExpanded = true
                };

                foreach (var step in group
                    .OrderBy(s => s.Mode)
                    .ThenBy(s => s.Rank)
                    .ThenBy(s => s.TypeName, StringComparer.OrdinalIgnoreCase))
                {
                    var isMatch = matchedIds.Contains(step.StepId);
                    var isSelectable = isMatch && IsAssemblySelected(step.Assembly);
                    var title = $"{step.TypeName} [{step.MessageName} / {step.PrimaryEntity}; Mode {step.Mode}, Rank {step.Rank}]";
                    if (!isMatch)
                    {
                        title = $"(candidate) {title}";
                    }

                    var stepNode = new ExecutionTreeNodeItem(title, step)
                    {
                        IsSelectable = isSelectable,
                        ToolTip = step.Display,
                        IsExpanded = true
                    };

                    AppendFilteredAttributesNode(stepNode, step);
                    AppendImageNodes(stepNode, step);
                    stageNode.Children.Add(stepNode);
                }

                if (stageNode.Children.Count > 0)
                {
                    root.Children.Add(stageNode);
                }
            }
        }

        private void AppendImageNodes(ExecutionTreeNodeItem stepNode, StepInfoModel step)
        {
            if (stepNode == null || step == null)
            {
                return;
            }

            var images = GetStepImages(step.StepId)
                .OrderBy(img => GetImageOrder(img.ImageType))
                .ThenBy(img => img.EntityAlias ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (images.Count == 0)
            {
                return;
            }

            var label = images.Count == 1 ? "Registered image" : $"Registered images ({images.Count})";
            var imagesNode = new ExecutionTreeNodeItem(label)
            {
                IsSelectable = false,
                ToolTip = "Plugin registration images",
                IsExpanded = true
            };

            foreach (var image in images)
            {
                var imageNode = new ExecutionTreeNodeItem(BuildImageTreeTitle(image))
                {
                    IsSelectable = false,
                    ToolTip = BuildImageTooltip(image),
                    IsExpanded = true
                };

                var attributes = ParseAttributeList(image.Attributes);
                var attrLabel = attributes.Count == 0
                    ? "Attributes (all columns)"
                    : $"Attributes ({attributes.Count})";
                var attrNode = BuildAttributeGroupNode(
                    attrLabel,
                    attributes,
                    "(all columns included)",
                    "Columns captured by this image");
                imageNode.Children.Add(attrNode);

                imagesNode.Children.Add(imageNode);
            }

            stepNode.Children.Add(imagesNode);
        }

        private void AppendFilteredAttributesNode(ExecutionTreeNodeItem stepNode, StepInfoModel step)
        {
            if (stepNode == null || step == null)
            {
                return;
            }

            var filters = ParseAttributeList(step.FilteringAttributes);
            var label = $"Filtered Attributes ({filters.Count})";
            var filteredNode = BuildAttributeGroupNode(
                label,
                filters,
                "(no filtering attributes)",
                "Columns configured on the plugin step");

            stepNode.Children.Insert(0, filteredNode);
        }

        private static ExecutionTreeNodeItem BuildAttributeGroupNode(
            string label,
            System.Collections.Generic.IReadOnlyCollection<string> attributes,
            string emptyMessage,
            string? tooltip)
        {
            var groupNode = new ExecutionTreeNodeItem(label)
            {
                IsSelectable = false,
                IsExpanded = false,
                ToolTip = tooltip
            };

            if (attributes.Count == 0)
            {
                groupNode.Children.Add(new ExecutionTreeNodeItem(emptyMessage)
                {
                    IsSelectable = false,
                    IsExpanded = false
                });
                return groupNode;
            }

            foreach (var attribute in attributes)
            {
                groupNode.Children.Add(new ExecutionTreeNodeItem(attribute)
                {
                    IsSelectable = false,
                    IsExpanded = false
                });
            }

            return groupNode;
        }

        private void OnExecutionTreeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ExecutionTree?.SelectedItem is ExecutionTreeNodeItem node && node.Step != null && node.IsSelectable)
            {
                _selectedTreeStep = node.Step;
                UpdateDebugButtonState();
                OnDebugMatchedStep(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void OnExecutionTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ExecutionTreeNodeItem node && node.Step != null && node.IsSelectable)
            {
                _selectedTreeStep = node.Step;
                _stepSetter?.Invoke(node.Step);
            }
            else
            {
                _selectedTreeStep = null;
            }

            UpdateDebugButtonState();
        }

        private static string StageLabel(int stage)
        {
            return stage switch
            {
                10 => "Stage 10 (PreValidation)",
                20 => "Stage 20 (PreOperation)",
                30 => "Stage 30 (Operation)",
                40 => "Stage 40 (PostOperation)",
                _ => $"Stage {stage}"
            };
        }

        private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Requests view no longer exposes capture filters.
        }

        private void OnSaveRequests(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = $"requests-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var list = new System.Collections.Generic.List<RequestDto>();
                foreach (var req in Requests)
                {
                    list.Add(RequestDto.From(req));
                }

                var json = System.Text.Json.JsonSerializer.Serialize(list, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save requests: {ex.Message}", "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnLoadRequests(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<RequestDto>>(json);
                if (list == null) return;
                Requests.Clear();
                foreach (var dto in list)
                {
                    Requests.Add(dto.ToModel());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load requests: {ex.Message}", "Load failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private sealed class RequestDto
        {
            public string? Method { get; set; }
            public string? OriginalUrl { get; set; }
            public string? Url { get; set; }
            public string? Headers { get; set; }
            public string? BodyPreview { get; set; }
            public string? BodyBase64 { get; set; }
            public string? ClientRequestId { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> HeadersDictionary { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public int? ResponseStatus { get; set; }
            public string? ResponseBodyPreview { get; set; }
            public System.Collections.Generic.List<string>? ResponseTraceLines { get; set; }
            public int MatchingTypesCount { get; set; }
            public bool AutoProxied { get; set; }
            public bool CanConvert { get; set; }
            public bool HasSteps { get; set; }

            public static RequestDto From(CapturedRequest req) => new RequestDto
            {
                Method = req.Method,
                OriginalUrl = req.OriginalUrl,
                Url = req.Url,
                Headers = req.Headers,
                BodyPreview = req.BodyPreview,
                BodyBase64 = req.Body != null ? Convert.ToBase64String(req.Body) : null,
                ClientRequestId = req.ClientRequestId,
                Timestamp = req.Timestamp,
                HeadersDictionary = req.HeadersDictionary,
                ResponseStatus = req.ResponseStatus,
                ResponseBodyPreview = req.ResponseBodyPreview,
                ResponseTraceLines = req.ResponseTraceLines,
                MatchingTypesCount = req.MatchingTypesCount,
                AutoProxied = req.AutoProxied,
                CanConvert = req.CanConvert,
                HasSteps = req.HasSteps
            };

            public CapturedRequest ToModel() => new CapturedRequest
            {
                Method = Method ?? string.Empty,
                OriginalUrl = OriginalUrl ?? string.Empty,
                Url = Url ?? string.Empty,
                Headers = Headers ?? string.Empty,
                BodyPreview = BodyPreview ?? string.Empty,
                Body = string.IsNullOrWhiteSpace(BodyBase64) ? null : Convert.FromBase64String(BodyBase64),
                ClientRequestId = ClientRequestId,
                Timestamp = Timestamp,
                HeadersDictionary = HeadersDictionary ?? new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase),
                ResponseStatus = ResponseStatus,
                ResponseBodyPreview = ResponseBodyPreview,
                ResponseTraceLines = ResponseTraceLines ?? new System.Collections.Generic.List<string>(),
                MatchingTypesCount = MatchingTypesCount,
                AutoProxied = AutoProxied,
                CanConvert = CanConvert,
                HasSteps = HasSteps
            };
        }

        public void ApplyCatalog(PluginCatalog catalog)
        {
            _catalog = catalog;
            CatalogSteps.Clear();
            if (catalog == null) return;
            foreach (var step in catalog.Steps)
            {
                var type = catalog.Types.FirstOrDefault(t => t.Id == step.PluginTypeId);
                CatalogSteps.Add(new StepInfoModel
                {
                    StepId = step.Id,
                    Assembly = type?.AssemblyName ?? string.Empty,
                    TypeName = type?.TypeName ?? string.Empty,
                    MessageName = step.Message,
                    PrimaryEntity = step.PrimaryEntity,
                    Stage = step.Stage,
                    Mode = step.Mode,
                    Rank = step.Rank,
                    FilteringAttributes = step.FilteringAttributes ?? string.Empty,
                    UnsecureConfiguration = step.UnsecureConfiguration,
                    SecureConfiguration = step.SecureConfiguration
                });
            }
            _imagesByStepId.Clear();
            if (catalog.Images != null)
            {
                foreach (var image in catalog.Images)
                {
                    if (!_imagesByStepId.TryGetValue(image.StepId, out var list))
                    {
                        list = new System.Collections.Generic.List<PluginImageItem>();
                        _imagesByStepId[image.StepId] = list;
                    }
                    list.Add(image);
                }
            }

            // Re-evaluate matches for existing requests so indicators show without selection
            foreach (var req in Requests)
            {
                var matches = GetMatches(req, log: false);
                var candidates = GetCandidateSteps(req, log: false);
                req.MatchingTypesCount = CountSelectableTypes(matches);
                req.HasSteps = candidates.Count > 0;
                UpdateConversionState(req);
            }
            RequestList.Items.Refresh();
            RefreshRequestFilter();

            if (RequestList.SelectedItem is CapturedRequest selected)
            {
                TryMapToStep(selected);
            }
        }

        public void ApplyEntityMap(System.Collections.Generic.Dictionary<string, string> map)
        {
            _entityMap = map ?? new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            _logicalToEntitySet = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _entityMap)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    _logicalToEntitySet[kvp.Value] = kvp.Key;
                }
            }
        }

        public void ApplyEnvironment(EnvironmentProfile profile)
        {
            _activeProfile = profile;
            _activeToken = profile.LastAccessToken;
        }

        public void ApplyMetadataPath(string? path)
        {
            _metadataPath = path;
            _requestParser.SetMetadataPath(path);
            foreach (var req in Requests)
            {
                UpdateConversionState(req);
            }
            RequestList.Items.Refresh();
            RefreshRequestFilter();
        }

        public void ApplyOperationSnapshot(string? path)
        {
            _operationSources.Clear();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                var json = File.ReadAllText(path);
                var snapshot = JsonSerializer.Deserialize<OperationParameterSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (snapshot == null)
                {
                    return;
                }

                if (snapshot.OperationSources != null && snapshot.OperationSources.Count > 0)
                {
                    foreach (var hint in snapshot.OperationSources)
                    {
                        RegisterOperationSourceHint(hint?.OperationName, hint?.Source);
                    }
                }

                if ((_operationSources.Count == 0) && snapshot.Parameters != null)
                {
                    foreach (var parameter in snapshot.Parameters)
                    {
                        if (parameter == null)
                        {
                            continue;
                        }

                        RegisterOperationSourceHint(parameter.OperationName, parameter.Source);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Append($"Operation snapshot load failed: {ex.Message}");
            }
        }

        private void RegisterOperationSourceHint(string? operationName, OperationParameterSource? source)
        {
            if (string.IsNullOrWhiteSpace(operationName) || !source.HasValue)
            {
                return;
            }

            _operationSources[operationName] = source.Value;
        }

        private void UpdateConversionState(CapturedRequest req)
        {
            if (req == null)
            {
                return;
            }

            var url = !string.IsNullOrWhiteSpace(req.OriginalUrl) ? req.OriginalUrl : req.Url;
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(req.Method))
            {
                req.CanConvert = false;
                return;
            }

            req.CanConvert = _requestParser.TryParse(req.Method, url, out _, out _);
        }

        public void ApplyAssemblyPaths(System.Collections.Generic.IEnumerable<string>? paths)
        {
            _assemblyPaths.Clear();
            _selectedAssemblyNames.Clear();
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    _assemblyPaths[path] = path;
                    var file = Path.GetFileName(path);
                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        _assemblyPaths[file] = path;
                        _selectedAssemblyNames.Add(file);
                    }
                    var fileNoExt = Path.GetFileNameWithoutExtension(path);
                    if (!string.IsNullOrWhiteSpace(fileNoExt))
                    {
                        _assemblyPaths[fileNoExt] = path;
                        _selectedAssemblyNames.Add(fileNoExt);
                    }
                }
            }

            foreach (var req in Requests)
            {
                var matches = GetMatches(req, log: false);
                var candidates = GetCandidateSteps(req, log: false);
                req.MatchingTypesCount = CountSelectableTypes(matches);
                req.HasSteps = candidates.Count > 0;
            }
            RequestList.Items.Refresh();
            RefreshRequestFilter();

            if (RequestList.SelectedItem is CapturedRequest selected)
            {
                TryMapToStep(selected);
            }
        }

        private bool IsAssemblySelected(string? assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return false;
            if (_selectedAssemblyNames.Count == 0) return false;

            if (_selectedAssemblyNames.Contains(assemblyName)) return true;

            var normalized = Path.GetFileNameWithoutExtension(assemblyName);
            if (!string.IsNullOrWhiteSpace(normalized) && _selectedAssemblyNames.Contains(normalized))
            {
                return true;
            }

            return false;
        }

        private string ResolveAssemblyPath(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName)) return string.Empty;
            if (assemblyName.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                assemblyName.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                return assemblyName;
            }

            if (_assemblyPaths.TryGetValue(assemblyName, out var path))
            {
                return path;
            }

            var noExt = Path.GetFileNameWithoutExtension(assemblyName);
            if (!string.IsNullOrWhiteSpace(noExt) && _assemblyPaths.TryGetValue(noExt, out path))
            {
                return path;
            }

            return string.Empty;
        }

        private void OnRequestsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (var obj in e.NewItems)
                {
                    if (obj is CapturedRequest req)
                    {
                        var matches = GetMatches(req, log: false);
                        var candidates = GetCandidateSteps(req, log: false);
                        req.MatchingTypesCount = CountSelectableTypes(matches);
                        req.HasSteps = candidates.Count > 0;
                        UpdateConversionState(req);
                    }
                }
                Dispatcher.InvokeAsync(() =>
                {
                    RequestList.Items.Refresh();
                    RefreshRequestFilter();
                });
            }
        }
    }
}
