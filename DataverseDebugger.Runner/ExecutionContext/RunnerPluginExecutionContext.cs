using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.ExecutionContext
{
    internal sealed class RunnerPluginExecutionContext : IPluginExecutionContext
    {
        public RunnerPluginExecutionContext()
        {
            InputParameters = new ParameterCollection();
            OutputParameters = new ParameterCollection();
            SharedVariables = new ParameterCollection();
            PreEntityImages = new EntityImageCollection();
            PostEntityImages = new EntityImageCollection();
            CorrelationId = Guid.NewGuid();
            OperationId = Guid.NewGuid();
            OperationCreatedOn = DateTime.UtcNow;
            RequestId = Guid.NewGuid();
            OwningExtension = new EntityReference("none", Guid.Empty);
        }

        public int Stage { get; set; }

        public IPluginExecutionContext? ParentContext { get; set; }

        public int Mode { get; set; }

        public int IsolationMode { get; set; }

        public int Depth { get; set; }

        public string MessageName { get; set; } = string.Empty;

        public string PrimaryEntityName { get; set; } = string.Empty;

        public Guid? RequestId { get; set; }

        public string SecondaryEntityName { get; set; } = string.Empty;

        public ParameterCollection InputParameters { get; }

        public ParameterCollection OutputParameters { get; }

        public ParameterCollection SharedVariables { get; }

        public Guid UserId { get; set; }

        public Guid InitiatingUserId { get; set; }

        public Guid BusinessUnitId { get; set; }

        public Guid OrganizationId { get; set; }

        public string OrganizationName { get; set; } = string.Empty;

        public Guid PrimaryEntityId { get; set; }

        public EntityImageCollection PreEntityImages { get; }

        public EntityImageCollection PostEntityImages { get; }

        public EntityReference OwningExtension { get; set; }

        public Guid CorrelationId { get; set; }

        public bool IsExecutingOffline { get; set; }

        public bool IsOfflinePlayback { get; set; }

        public bool IsInTransaction { get; set; }

        public Guid OperationId { get; set; }

        public DateTime OperationCreatedOn { get; set; }
    }
}
