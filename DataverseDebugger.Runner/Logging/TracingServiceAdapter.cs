using System;
using Microsoft.Xrm.Sdk;

namespace DataverseDebugger.Runner.Logging
{
    internal sealed class TracingServiceAdapter : ITracingService
    {
        private readonly IRunnerLogger _logger;

        public TracingServiceAdapter(IRunnerLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Trace(string format, params object[] args)
        {
            var message = format ?? string.Empty;
            if (args != null && args.Length > 0)
            {
                try
                {
                    message = string.Format(format ?? string.Empty, args);
                }
                catch (FormatException)
                {
                    message = format ?? string.Empty;
                }
            }

            _logger.Log(new RunnerLogEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Message = message
            });
        }
    }
}
