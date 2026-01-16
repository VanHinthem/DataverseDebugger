using DataverseDebugger.Protocol;
using DataverseDebugger.Runner.Pipeline;

namespace DataverseDebugger.Runner.EntryAdapters
{
    internal sealed class WebApiEntryAdapter
    {
        public ExecutionRequest Build(PluginInvokeRequest request)
        {
            return new ExecutionRequest
            {
                RequestId = request?.RequestId ?? string.Empty
            };
        }
    }
}
