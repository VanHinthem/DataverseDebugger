#if NET48
using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner
{
    internal sealed class HybridReadTestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var orgService = serviceProvider?.GetService(typeof(IOrganizationService)) as IOrganizationService;
            var context = serviceProvider?.GetService(typeof(IPluginExecutionContext)) as IPluginExecutionContext;
            var tracing = serviceProvider?.GetService(typeof(ITracingService)) as ITracingService;

            if (orgService != null && context != null)
            {
                var id = context.PrimaryEntityId;
                var entity = orgService.Retrieve("account", id, new ColumnSet("name"));
                var name = entity.GetAttributeValue<string>("name") ?? "(null)";
                tracing?.Trace("HybridReadName={0}", name);
            }
        }
    }
}
#endif
