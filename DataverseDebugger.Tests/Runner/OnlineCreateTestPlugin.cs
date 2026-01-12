#if NET48
using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Tests.Runner
{
    internal sealed class OnlineCreateTestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var orgService = serviceProvider?.GetService(typeof(IOrganizationService)) as IOrganizationService;
            var tracing = serviceProvider?.GetService(typeof(ITracingService)) as ITracingService;

            if (orgService != null)
            {
                var entity = new Entity("account");
                entity["name"] = "online";
                var id = orgService.Create(entity);
                tracing?.Trace("OnlineCreateId={0}", id);
            }
        }
    }
}
#endif
