using DataverseDebugger.Runner.Pipeline;

namespace DataverseDebugger.Runner.ExecutionContext
{
    internal interface IExecutionContextBuilder
    {
        RunnerPluginExecutionContext Build(ExecutionRequest request);
    }
}
