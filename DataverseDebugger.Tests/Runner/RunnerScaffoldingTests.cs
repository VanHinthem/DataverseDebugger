using System;
using System.IO;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.Tests.Runner;

[TestClass]
public sealed class RunnerScaffoldingTests
{
    [TestMethod]
    public void RunnerScaffoldingFoldersExist()
    {
        var root = GetRepoRoot();
        var runnerRoot = Path.Combine(root, "DataverseDebugger.Runner");

        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Abstractions")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Configuration")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Pipeline")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "EntryAdapters")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "ExecutionContext")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Logging")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Services", "Offline")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Services", "Hybrid")));
        Assert.IsTrue(Directory.Exists(Path.Combine(runnerRoot, "Services", "Live")));
    }

    [TestMethod]
    public void RunnerScaffoldingFilesExist()
    {
        var root = GetRepoRoot();
        var runnerRoot = Path.Combine(root, "DataverseDebugger.Runner");

        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Abstractions", "ExecutionMode.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Abstractions", "EntryMechanism.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Abstractions", "RunnerNotSupportedException.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Configuration", "RunnerExecutionOptions.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Pipeline", "ExecutionRequest.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Pipeline", "ExecutionResult.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Pipeline", "PluginInvocationEngine.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "EntryAdapters", "WebApiEntryAdapter.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "EntryAdapters", "ProfilerEntryAdapter.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "ExecutionContext", "RunnerPluginExecutionContext.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "ExecutionContext", "IExecutionContextBuilder.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "ExecutionContext", "WebApiContextBuilder.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "ExecutionContext", "ProfilerContextBuilder.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Logging", "RunnerLogEntry.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Logging", "IRunnerLogger.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Logging", "RunnerLogger.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Logging", "TracingServiceAdapter.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Services", "Offline", "OfflineOrganizationService.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Services", "Hybrid", "HybridWriteCache.cs")));
        Assert.IsTrue(File.Exists(Path.Combine(runnerRoot, "Services", "Hybrid", "EntityMergeUtility.cs")));
    }

    [TestMethod]
    public void PluginInvokeRequest_ExecutionMode_RoundTrips()
    {
        var request = new PluginInvokeRequest
        {
            ExecutionMode = "Online",
            WriteMode = "LiveWrites"
        };

        Assert.AreEqual("Online", request.ExecutionMode);
        Assert.AreEqual("LiveWrites", request.WriteMode);
        Assert.AreNotEqual(Guid.Empty.ToString("N"), request.RequestId);
    }

    private static string GetRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "DataverseDebugger.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        Assert.Fail($"Unable to locate repository root from '{AppContext.BaseDirectory}'.");
        return string.Empty;
    }
}
