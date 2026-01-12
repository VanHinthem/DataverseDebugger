#if NET48
using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Tests.Runner
{
    internal sealed class MinimalTestPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var tracing = serviceProvider?.GetService(typeof(ITracingService)) as ITracingService;
            var context = serviceProvider?.GetService(typeof(IPluginExecutionContext)) as IPluginExecutionContext;

            if (context != null)
            {
                context.OutputParameters["Result"] = "Expected";
            }

            tracing?.Trace("MinimalTestPlugin executed");
            var result = context != null && context.OutputParameters.Contains("Result")
                ? context.OutputParameters["Result"]
                : null;
            tracing?.Trace("Output:Result={0}", result ?? "(null)");
        }
    }
}
#endif
