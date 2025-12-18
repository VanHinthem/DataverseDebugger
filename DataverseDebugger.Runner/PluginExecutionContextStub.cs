using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

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
            _services.TryGetValue(serviceType, out var service);
            return service;
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
}
