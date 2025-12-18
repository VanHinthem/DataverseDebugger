using System;
using System.Collections.Generic;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.Runner
{
    /// <summary>
    /// Provides centralized logging for the runner process.
    /// Log entries are stored in a circular buffer and can be fetched via IPC.
    /// </summary>
    internal static class RunnerLogger
    {
        private static readonly object LockObj = new object();
        private static readonly List<RunnerLogEntry> Entries = new List<RunnerLogEntry>();
        private static long _nextId = 1;
        private static RunnerLogLevel _level = RunnerLogLevel.Info;
        private static RunnerLogCategory _categories = RunnerLogCategory.All;
        private static int _maxEntries = 1000;

        /// <summary>
        /// Configures the logger with the specified settings.
        /// </summary>
        /// <param name="request">Configuration request with level, categories, and buffer size.</param>
        public static void Configure(RunnerLogConfigRequest? request)
        {
            if (request == null)
            {
                return;
            }

            lock (LockObj)
            {
                _level = request.Level;
                _categories = request.Categories;
                _maxEntries = Math.Max(200, Math.Min(5000, request.MaxEntries));
            }

            Log(RunnerLogCategory.RunnerLifecycle, RunnerLogLevel.Info,
                $"Runner logging configured: Level={_level}, Categories={_categories}, MaxEntries={_maxEntries}");
        }

        /// <summary>
        /// Logs a message with the specified category and level.
        /// </summary>
        /// <param name="category">Log category for filtering.</param>
        /// <param name="level">Log verbosity level.</param>
        /// <param name="message">Message to log.</param>
        public static void Log(RunnerLogCategory category, RunnerLogLevel level, string message)
        {
            if (!IsEnabled(category, level))
            {
                return;
            }

            var entry = new RunnerLogEntry
            {
                Id = GetNextId(),
                TimestampUtc = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message ?? string.Empty
            };

            lock (LockObj)
            {
                Entries.Add(entry);
                if (Entries.Count > _maxEntries)
                {
                    var removeCount = Entries.Count - _maxEntries;
                    Entries.RemoveRange(0, removeCount);
                }
            }
        }

        /// <summary>
        /// Fetches log entries newer than the specified ID.
        /// </summary>
        /// <param name="request">Fetch request with last seen ID and max entries.</param>
        /// <returns>Response containing matching log lines.</returns>
        public static RunnerLogFetchResponse Fetch(RunnerLogFetchRequest request)
        {
            var response = new RunnerLogFetchResponse
            {
                Version = ProtocolVersion.Current,
                LastId = request.LastId
            };

            lock (LockObj)
            {
                var max = request.MaxEntries <= 0 ? 200 : request.MaxEntries;
                var matches = new List<RunnerLogEntry>();
                foreach (var entry in Entries)
                {
                    if (entry.Id > request.LastId)
                    {
                        matches.Add(entry);
                        if (matches.Count >= max)
                        {
                            break;
                        }
                    }
                }

                if (matches.Count > 0)
                {
                    response.LastId = matches[matches.Count - 1].Id;
                    foreach (var entry in matches)
                    {
                        response.Lines.Add(Format(entry));
                    }
                }
            }

            return response;
        }

        private static bool IsEnabled(RunnerLogCategory category, RunnerLogLevel level)
        {
            lock (LockObj)
            {
                if ((_categories & category) == 0)
                {
                    return false;
                }

                return level <= _level;
            }
        }

        private static long GetNextId()
        {
            lock (LockObj)
            {
                return _nextId++;
            }
        }

        private static string Format(RunnerLogEntry entry)
        {
            var timestamp = entry.TimestampUtc.ToString("HH:mm:ss");
            return $"{timestamp} [{entry.Level}] [{entry.Category}] {entry.Message}";
        }

        /// <summary>
        /// Internal log entry structure.
        /// </summary>
        private sealed class RunnerLogEntry
        {
            public long Id { get; set; }
            public DateTime TimestampUtc { get; set; }
            public RunnerLogLevel Level { get; set; }
            public RunnerLogCategory Category { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}
