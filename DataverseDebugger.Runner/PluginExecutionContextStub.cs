using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using ExtEventId = Microsoft.Extensions.Logging.EventId;
using ExtLogLevel = Microsoft.Extensions.Logging.LogLevel;
using ExtLogger = Microsoft.Extensions.Logging.ILogger;
using ExtLoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;
using ExtLoggerProvider = Microsoft.Extensions.Logging.ILoggerProvider;
using PluginEventId = Microsoft.Xrm.Sdk.PluginTelemetry.EventId;
using PluginLogLevel = Microsoft.Xrm.Sdk.PluginTelemetry.LogLevel;
using PluginLogger = Microsoft.Xrm.Sdk.PluginTelemetry.ILogger;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Service provider stub that returns registered services by type.
    /// Used to provide plugin execution dependencies.
    /// </summary>
    internal sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services;

        /// <summary>
        /// Initializes a new instance with the specified service mappings.
        /// </summary>
        /// <param name="services">Dictionary mapping service types to implementations.</param>
        public StubServiceProvider(Dictionary<Type, object> services)
        {
            _services = services;
        }

        /// <inheritdoc />
        public object? GetService(Type serviceType)
        {
            if (serviceType == null)
            {
                return null;
            }

            if (_services.TryGetValue(serviceType, out var service))
            {
                return service;
            }

            foreach (var candidate in _services.Values)
            {
                if (candidate != null && serviceType.IsInstanceOfType(candidate))
                {
                    return candidate;
                }
            }

            var requestedName = serviceType.FullName;
            if (!string.IsNullOrEmpty(requestedName))
            {
                foreach (var pair in _services)
                {
                    if (string.Equals(pair.Key.FullName, requestedName, StringComparison.Ordinal))
                    {
                        return pair.Value;
                    }
                }
            }

            if (serviceType.IsGenericType)
            {
                var definition = serviceType.GetGenericTypeDefinition();
                if (definition == typeof(Microsoft.Extensions.Logging.ILogger<>)
                    && _services.TryGetValue(typeof(ExtLoggerFactory), out var factoryObj)
                    && factoryObj is ExtLoggerFactory loggerFactory)
                {
                    var genericType = serviceType.GetGenericArguments()[0];
                    var category = genericType.FullName ?? genericType.Name;
                    return loggerFactory.CreateLogger(category);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Minimal ILogger implementation that bridges log messages into the runner trace list.
    /// </summary>
    internal sealed class StubLogger : ExtLogger
    {
        private readonly string _categoryName;
        private readonly Action<string> _log;

        public StubLogger(string categoryName, Action<string> log)
        {
            _categoryName = categoryName;
            _log = log;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return NoopScope.Instance;
        }

        public bool IsEnabled(ExtLogLevel logLevel) => true;

        public void Log<TState>(ExtLogLevel logLevel, ExtEventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string>? formatter)
        {
            var message = formatter != null
                ? formatter(state, exception)
                : state?.ToString() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(message))
            {
                _log($"[ILogger:{_categoryName}] [{logLevel}] {message}");
            }

            if (exception != null)
            {
                _log(exception.ToString());
            }
        }
    }

    /// <summary>
    /// Minimal ILoggerFactory implementation that creates StubLogger instances per category.
    /// </summary>
    internal sealed class StubLoggerFactory : ExtLoggerFactory
    {
        private readonly Action<string> _log;

        public StubLoggerFactory(Action<string> log)
        {
            _log = log;
        }

        public void AddProvider(ExtLoggerProvider provider)
        {
            // not needed for local debugging
        }

        public ExtLogger CreateLogger(string categoryName)
        {
            var category = string.IsNullOrWhiteSpace(categoryName) ? "Plugin" : categoryName;
            return new StubLogger(category, _log);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Minimal implementation of Microsoft.Xrm.Sdk.PluginTelemetry.ILogger.
    /// Bridges telemetry log calls into the runner trace output so plugins depending on the interface keep working.
    /// </summary>
    internal sealed class StubPluginTelemetryLogger : PluginLogger
    {
        private readonly Action<string> _log;
        private readonly Dictionary<string, string> _customProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public StubPluginTelemetryLogger(Action<string> log)
        {
            _log = log;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NoopScope.Instance;
        }

        public IDisposable BeginScope(string messageFormat, params object[] args)
        {
            var message = FormatMessage(messageFormat, args);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _log($"[PluginTelemetry] Scope → {message}");
            }
            return NoopScope.Instance;
        }

        public bool IsEnabled(PluginLogLevel logLevel) => true;

        public void Log<TState>(PluginLogLevel logLevel, PluginEventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            var message = formatter != null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
            Write(logLevel, eventId, message, exception);
        }

        public void Log(PluginLogLevel logLevel, PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(logLevel, eventId, FormatMessage(message, args), exception);
        }

        public void Log(PluginLogLevel logLevel, PluginEventId eventId, string message, params object[] args)
        {
            Write(logLevel, eventId, FormatMessage(message, args), null);
        }

        public void Log(PluginLogLevel logLevel, Exception exception, string message, params object[] args)
        {
            Write(logLevel, null, FormatMessage(message, args), exception);
        }

        public void Log(PluginLogLevel logLevel, string message, params object[] args)
        {
            Write(logLevel, null, FormatMessage(message, args), null);
        }

        public void LogCritical(PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Critical, eventId, FormatMessage(message, args), exception);
        }

        public void LogCritical(PluginEventId eventId, string message, params object[] args)
        {
            Write(PluginLogLevel.Critical, eventId, FormatMessage(message, args), null);
        }

        public void LogCritical(Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Critical, null, FormatMessage(message, args), exception);
        }

        public void LogCritical(string message, params object[] args)
        {
            Write(PluginLogLevel.Critical, null, FormatMessage(message, args), null);
        }

        public void LogDebug(PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Debug, eventId, FormatMessage(message, args), exception);
        }

        public void LogDebug(PluginEventId eventId, string message, params object[] args)
        {
            Write(PluginLogLevel.Debug, eventId, FormatMessage(message, args), null);
        }

        public void LogDebug(Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Debug, null, FormatMessage(message, args), exception);
        }

        public void LogDebug(string message, params object[] args)
        {
            Write(PluginLogLevel.Debug, null, FormatMessage(message, args), null);
        }

        public void LogError(PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Error, eventId, FormatMessage(message, args), exception);
        }

        public void LogError(PluginEventId eventId, string message, params object[] args)
        {
            Write(PluginLogLevel.Error, eventId, FormatMessage(message, args), null);
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Error, null, FormatMessage(message, args), exception);
        }

        public void LogError(string message, params object[] args)
        {
            Write(PluginLogLevel.Error, null, FormatMessage(message, args), null);
        }

        public void LogInformation(PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Information, eventId, FormatMessage(message, args), exception);
        }

        public void LogInformation(PluginEventId eventId, string message, params object[] args)
        {
            Write(PluginLogLevel.Information, eventId, FormatMessage(message, args), null);
        }

        public void LogInformation(Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Information, null, FormatMessage(message, args), exception);
        }

        public void LogInformation(string message, params object[] args)
        {
            Write(PluginLogLevel.Information, null, FormatMessage(message, args), null);
        }

        public void LogTrace(PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Trace, eventId, FormatMessage(message, args), exception);
        }

        public void LogTrace(PluginEventId eventId, string message, params object[] args)
        {
            Write(PluginLogLevel.Trace, eventId, FormatMessage(message, args), null);
        }

        public void LogTrace(Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Trace, null, FormatMessage(message, args), exception);
        }

        public void LogTrace(string message, params object[] args)
        {
            Write(PluginLogLevel.Trace, null, FormatMessage(message, args), null);
        }

        public void LogWarning(PluginEventId eventId, Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Warning, eventId, FormatMessage(message, args), exception);
        }

        public void LogWarning(PluginEventId eventId, string message, params object[] args)
        {
            Write(PluginLogLevel.Warning, eventId, FormatMessage(message, args), null);
        }

        public void LogWarning(Exception exception, string message, params object[] args)
        {
            Write(PluginLogLevel.Warning, null, FormatMessage(message, args), exception);
        }

        public void LogWarning(string message, params object[] args)
        {
            Write(PluginLogLevel.Warning, null, FormatMessage(message, args), null);
        }

        public void LogMetric(string metricName, long value)
        {
            var name = string.IsNullOrWhiteSpace(metricName) ? "metric" : metricName;
            _log($"[PluginTelemetry] Metric {name} = {value}");
        }

        public void LogMetric(string metricName, IDictionary<string, string> metricDimensions, long value)
        {
            var name = string.IsNullOrWhiteSpace(metricName) ? "metric" : metricName;
            var dims = metricDimensions != null && metricDimensions.Count > 0
                ? $" dims={string.Join(", ", FormatDimensionPairs(metricDimensions))}"
                : string.Empty;
            _log($"[PluginTelemetry] Metric {name} = {value}{dims}");
        }

        public void AddCustomProperty(string propertyName, string propertyValue)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            _customProperties[propertyName] = propertyValue ?? string.Empty;
        }

        public void Execute(string activityName, Action action, IEnumerable<KeyValuePair<string, string>> additionalCustomProperties)
        {
            AppendCustomProperties(additionalCustomProperties);
            var name = activityName ?? "activity";
            Write(PluginLogLevel.Trace, null, $"Execute {name} (sync) started", null);
            try
            {
                action?.Invoke();
                Write(PluginLogLevel.Trace, null, $"Execute {name} (sync) completed", null);
            }
            catch (Exception ex)
            {
                Write(PluginLogLevel.Error, null, $"Execute {name} (sync) failed", ex);
                throw;
            }
        }

        public Task ExecuteAsync(string activityName, Func<Task> action, IEnumerable<KeyValuePair<string, string>> additionalCustomProperties)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            return ExecuteAsyncCore(activityName, action, additionalCustomProperties);
        }

        private async Task ExecuteAsyncCore(string activityName, Func<Task> action, IEnumerable<KeyValuePair<string, string>> additionalCustomProperties)
        {
            AppendCustomProperties(additionalCustomProperties);
            var name = activityName ?? "activity";
            Write(PluginLogLevel.Trace, null, $"Execute {name} (async) started", null);
            try
            {
                await action().ConfigureAwait(false);
                Write(PluginLogLevel.Trace, null, $"Execute {name} (async) completed", null);
            }
            catch (Exception ex)
            {
                Write(PluginLogLevel.Error, null, $"Execute {name} (async) failed", ex);
                throw;
            }
        }

        private void Write(PluginLogLevel level, PluginEventId? eventId, string? message, Exception? exception)
        {
            var eventSuffix = eventId != null ? $" event={FormatEventId(eventId)}" : string.Empty;
            var propsSuffix = _customProperties.Count > 0
                ? $" props={string.Join(", ", FormatCustomProperties())}"
                : string.Empty;
            var text = string.IsNullOrWhiteSpace(message) ? "(no message)" : message;
            _log($"[PluginTelemetry:{level}]{eventSuffix}{propsSuffix} {text}");

            if (exception != null)
            {
                _log(exception.ToString());
            }
        }

        private static string FormatMessage(string message, params object[] args)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return message;
            }

            try
            {
                return string.Format(message, args);
            }
            catch (FormatException)
            {
                return message;
            }
        }

        private static string FormatEventId(PluginEventId eventId)
        {
            var name = eventId.Name;
            return string.IsNullOrWhiteSpace(name) ? eventId.Id.ToString() : $"{eventId.Id}:{name}";
        }

        private void AppendCustomProperties(IEnumerable<KeyValuePair<string, string>>? additionalCustomProperties)
        {
            if (additionalCustomProperties == null)
            {
                return;
            }

            foreach (var pair in additionalCustomProperties)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                _customProperties[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        private IEnumerable<string> FormatCustomProperties()
        {
            foreach (var pair in _customProperties)
            {
                yield return $"{pair.Key}={pair.Value}";
            }
        }

        private static IEnumerable<string> FormatDimensionPairs(IDictionary<string, string> dimensions)
        {
            foreach (var pair in dimensions)
            {
                yield return $"{pair.Key}={pair.Value}";
            }
        }
    }

    /// <summary>
    /// Minimal IServiceEndpointNotificationService implementation used during local debugging.
    /// </summary>
    internal sealed class StubServiceEndpointNotificationService : IServiceEndpointNotificationService
    {
        private readonly Action<string> _log;

        public StubServiceEndpointNotificationService(Action<string> log)
        {
            _log = log;
        }

        public string Execute(EntityReference serviceEndpoint, IExecutionContext context)
        {
            var endpointName = serviceEndpoint?.LogicalName ?? "unknown";
            _log($"[IServiceEndpointNotificationService] Execute → {endpointName} ({serviceEndpoint?.Id})");
            return string.Empty;
        }
    }

    /// <summary>
    /// Minimal IFeatureControlService implementation; always reports feature flags as disabled.
    /// </summary>
    internal sealed class StubFeatureControlService : IFeatureControlService
    {
        private readonly Action<string> _log;

        public StubFeatureControlService(Action<string> log)
        {
            _log = log;
        }

        public object? GetFeatureControl(string namespaceValue, string featureControlName, out Type type)
        {
            _log($"[IFeatureControlService] Request {namespaceValue}/{featureControlName} (returns false)");
            type = typeof(bool);
            return false;
        }
    }

    /// <summary>
    /// Tracing service stub that forwards trace output to a logging delegate.
    /// </summary>
    internal sealed class StubTracingService : ITracingService
    {
        private readonly Action<string> _log;

        /// <summary>
        /// Initializes a new instance with the specified logging delegate.
        /// </summary>
        /// <param name="log">Action to receive trace messages.</param>
        public StubTracingService(Action<string> log)
        {
            _log = log;
        }

        /// <inheritdoc />
        public void Trace(string format, params object[] args)
        {
            try
            {
                var message = args != null && args.Length > 0 ? string.Format(format, args) : format;
                _log(message);
            }
            catch
            {
                // ignore logging failures
            }
        }
    }

    /// <summary>
    /// Organization service factory stub that returns the same service instance for all requests.
    /// </summary>
    internal sealed class StubOrganizationServiceFactory : IOrganizationServiceFactory
    {
        private readonly IOrganizationService _service;

        /// <summary>
        /// Initializes a new instance with the specified service.
        /// </summary>
        /// <param name="service">The organization service to return.</param>
        public StubOrganizationServiceFactory(IOrganizationService service)
        {
            _service = service;
        }

        /// <inheritdoc />
        public IOrganizationService CreateOrganizationService(Guid? userId)
        {
            return _service;
        }
    }

    /// <summary>
    /// Simple organization service stub that simulates all operations.
    /// Does not perform any real Dataverse operations.
    /// </summary>
    internal sealed class StubOrganizationService : IOrganizationService
    {
        private readonly Action<string> _log;

        /// <summary>
        /// Initializes a new instance with the specified logging delegate.
        /// </summary>
        /// <param name="log">Action to receive operation logs.</param>
        public StubOrganizationService(Action<string> log)
        {
            _log = log;
        }

        /// <inheritdoc />
        public Guid Create(Entity entity)
        {
            _log($"[OrgService] Create {entity.LogicalName} (simulated)");
            return Guid.NewGuid();
        }

        public void Update(Entity entity)
        {
            _log($"[OrgService] Update {entity.LogicalName} (simulated)");
        }

        public void Delete(string entityName, Guid id)
        {
            _log($"[OrgService] Delete {entityName} {id} (simulated)");
        }

        public OrganizationResponse Execute(OrganizationRequest request)
        {
            _log($"[OrgService] Execute {request.RequestName} (simulated)");
            return new OrganizationResponse();
        }

        public Entity Retrieve(string entityName, Guid id, Microsoft.Xrm.Sdk.Query.ColumnSet columnSet)
        {
            _log($"[OrgService] Retrieve {entityName} {id} (simulated)");
            return new Entity(entityName) { Id = id };
        }

        public EntityCollection RetrieveMultiple(Microsoft.Xrm.Sdk.Query.QueryBase query)
        {
            _log("[OrgService] RetrieveMultiple (simulated)");
            return new EntityCollection();
        }

        public void Associate(string entityName, Guid entityId, Microsoft.Xrm.Sdk.Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            _log($"[OrgService] Associate {entityName} {entityId} (simulated)");
        }

        public void Disassociate(string entityName, Guid entityId, Microsoft.Xrm.Sdk.Relationship relationship, EntityReferenceCollection relatedEntities)
        {
            _log($"[OrgService] Disassociate {entityName} {entityId} (simulated)");
        }
    }

    /// <summary>
    /// Plugin execution context stub providing all context properties for plugin invocation.
    /// Implements the full IPluginExecutionContext interface with settable properties.
    /// </summary>
    internal sealed class StubPluginExecutionContext : IPluginExecutionContext
    {
        /// <inheritdoc />
        public int Stage { get; set; }

        /// <inheritdoc />
        public IPluginExecutionContext? ParentContext { get; set; }

        /// <inheritdoc />
        public int Mode { get; set; }

        /// <inheritdoc />
        public int IsolationMode { get; set; }

        /// <inheritdoc />
        public int Depth { get; set; }

        /// <inheritdoc />
        public string MessageName { get; set; } = string.Empty;

        /// <inheritdoc />
        public string PrimaryEntityName { get; set; } = string.Empty;

        /// <inheritdoc />
        public Guid? RequestId { get; set; }

        /// <inheritdoc />
        public string SecondaryEntityName { get; set; } = string.Empty;

        /// <inheritdoc />
        public ParameterCollection InputParameters { get; } = new ParameterCollection();

        /// <inheritdoc />
        public ParameterCollection OutputParameters { get; } = new ParameterCollection();

        /// <inheritdoc />
        public ParameterCollection SharedVariables { get; } = new ParameterCollection();

        /// <inheritdoc />
        public Guid UserId { get; set; }

        /// <inheritdoc />
        public Guid InitiatingUserId { get; set; }

        /// <inheritdoc />
        public Guid BusinessUnitId { get; set; }

        /// <inheritdoc />
        public Guid OrganizationId { get; set; }

        /// <inheritdoc />
        public string OrganizationName { get; set; } = string.Empty;

        /// <inheritdoc />
        public Guid PrimaryEntityId { get; set; }

        /// <inheritdoc />
        public EntityImageCollection PreEntityImages { get; } = new EntityImageCollection();

        /// <inheritdoc />
        public EntityImageCollection PostEntityImages { get; } = new EntityImageCollection();

        /// <inheritdoc />
        public EntityReference OwningExtension { get; set; } = new EntityReference("none", Guid.Empty);

        /// <inheritdoc />
        public Guid CorrelationId { get; set; }

        /// <inheritdoc />
        public bool IsExecutingOffline { get; set; }

        /// <inheritdoc />
        public bool IsOfflinePlayback { get; set; }

        /// <inheritdoc />
        public bool IsInTransaction { get; set; }

        /// <inheritdoc />
        public Guid OperationId { get; set; }

        /// <inheritdoc />
        public DateTime OperationCreatedOn { get; set; }

        /// <summary>
        /// Gets or sets whether this context is a clone.
        /// </summary>
        public bool IsClone { get; set; }
    }

    /// <summary>
    /// Shared no-op disposable scope used by our logger stubs.
    /// </summary>
    internal sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new NoopScope();

        public void Dispose()
        {
        }
    }
}
