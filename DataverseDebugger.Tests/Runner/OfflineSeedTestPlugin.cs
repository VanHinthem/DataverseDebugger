#if NET48
using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DataverseDebugger.Tests.Runner
{
    internal sealed class OfflineSeedTestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var tracing = serviceProvider?.GetService(typeof(ITracingService)) as ITracingService;
            var context = serviceProvider?.GetService(typeof(IPluginExecutionContext)) as IPluginExecutionContext;
            var orgService = serviceProvider?.GetService(typeof(IOrganizationService)) as IOrganizationService;

            var target = context?.InputParameters.Contains("Target") == true
                ? context.InputParameters["Target"] as Entity
                : null;

            var retrievedName = "(null)";
            if (orgService != null && target != null)
            {
                var retrieved = orgService.Retrieve(target.LogicalName, target.Id, new ColumnSet(true));
                var name = retrieved.GetAttributeValue<string>("name");
                retrievedName = string.IsNullOrWhiteSpace(name) ? "(null)" : name;
                if (context != null)
                {
                    context.OutputParameters["OfflineName"] = retrievedName;
                }
            }

            tracing?.Trace("OfflineName={0}", retrievedName);
        }
    }
}
#endif
