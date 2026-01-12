#if NET48
using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Tests.Runner
{
    internal sealed class DualConstructorPlugin : IPlugin
    {
        private readonly string? _unsecure;
        private readonly string? _secure;
        private readonly string _ctorTag;

        public DualConstructorPlugin(string unsecure, string secure)
        {
            _unsecure = unsecure;
            _secure = secure;
            _ctorTag = "dual";
        }

        public DualConstructorPlugin(string unsecure)
        {
            _unsecure = unsecure;
            _secure = null;
            _ctorTag = "single";
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            var tracing = serviceProvider?.GetService(typeof(ITracingService)) as ITracingService;
            tracing?.Trace("Ctor={0}", _ctorTag);
            tracing?.Trace("Unsecure={0}", _unsecure ?? "(null)");
            tracing?.Trace("Secure={0}", _secure ?? "(null)");
        }
    }

    internal sealed class SingleConstructorPlugin : IPlugin
    {
        private readonly string? _unsecure;

        public SingleConstructorPlugin(string unsecure)
        {
            _unsecure = unsecure;
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            var tracing = serviceProvider?.GetService(typeof(ITracingService)) as ITracingService;
            tracing?.Trace("Ctor=single");
            tracing?.Trace("Unsecure={0}", _unsecure ?? "(null)");
        }
    }
}
#endif
