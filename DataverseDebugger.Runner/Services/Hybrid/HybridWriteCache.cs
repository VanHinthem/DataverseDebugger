using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Services.Hybrid
{
    internal sealed class HybridWriteCache
    {
        public List<Entity> Creates { get; } = new List<Entity>();

        public List<Entity> Updates { get; } = new List<Entity>();

        public List<EntityReference> Deletes { get; } = new List<EntityReference>();
    }
}
