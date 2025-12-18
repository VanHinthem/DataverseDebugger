using System;
using System.IO;
using System.Text.Json;
using DataverseDebugger.App.Models;
using DataverseDebugger.Protocol;

namespace DataverseDebugger.App.Services
{
    /// <summary>
    /// Handles loading and saving application settings to JSON.
    /// </summary>
    /// <remarks>
    /// Settings are stored in settings/app-settings.json. Supports migration
    /// from legacy separate browser-settings.json and runner-log-settings.json files.
    /// </remarks>
    public static class AppSettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            AppContext.BaseDirectory,
            "settings",
            "app-settings.json");

        private static readonly string LegacyBrowserPath = Path.Combine(
            AppContext.BaseDirectory,
            "settings",
            "browser-settings.json");
        private static readonly string LegacyRunnerPath = Path.Combine(
            AppContext.BaseDirectory,
            "settings",
            "runner-log-settings.json");

        /// <summary>
        /// Loads application settings from disk, migrating legacy files if needed.
        /// </summary>
        /// <returns>The loaded settings model (defaults if no file exists).</returns>
        public static AppSettingsModel Load()
        {
            var model = new AppSettingsModel();
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var dto = JsonSerializer.Deserialize<AppSettingsDto>(json);
                    if (dto != null)
                    {
                        ApplyBrowser(model.Browser, dto.Browser);
                        ApplyRunner(model.RunnerLog, dto.RunnerLog);
                        ApplyRunnerSettings(model.Runner, dto.Runner);
                        ApplyAppearance(model.Appearance, dto.Appearance);
                    }
                    return model;
                }

                var legacyBrowser = ReadBrowserDto(LegacyBrowserPath);
                var legacyRunner = ReadRunnerDto(LegacyRunnerPath);

                if (legacyBrowser != null)
                {
                    ApplyBrowser(model.Browser, legacyBrowser);
                }

                if (legacyRunner != null)
                {
                    ApplyRunner(model.RunnerLog, legacyRunner);
                }

                if (legacyBrowser != null || legacyRunner != null)
                {
                    Save(model);
                    CleanupLegacyFiles();
                }
            }
            catch
            {
                // ignore load failures
            }

            return model;
        }

        public static void Save(BrowserSettingsModel browser, RunnerLogSettingsModel runnerLog)
        {
            var model = new AppSettingsModel();
            CopyBrowser(browser, model.Browser);
            CopyRunner(runnerLog, model.RunnerLog);
            Save(model);
        }

        public static void Save(BrowserSettingsModel browser, RunnerLogSettingsModel runnerLog, RunnerSettingsModel runnerSettings)
        {
            var model = new AppSettingsModel();
            CopyBrowser(browser, model.Browser);
            CopyRunner(runnerLog, model.RunnerLog);
            CopyRunnerSettings(runnerSettings, model.Runner);
            Save(model);
        }

        public static void Save(AppSettingsModel model)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var dto = new AppSettingsDto
                {
                    Browser = new BrowserSettingsDto
                    {
                        DisableCaching = model.Browser.DisableCaching,
                        BypassServiceWorker = model.Browser.BypassServiceWorker,
                        OpenDevToolsOnActivate = model.Browser.OpenDevToolsOnActivate
                    },
                    RunnerLog = new RunnerLogSettingsDto
                    {
                        Level = model.RunnerLog.Level,
                        Categories = model.RunnerLog.ToCategories()
                    },
                    Runner = new RunnerSettingsDto
                    {
                        WriteMode = model.Runner.WriteMode
                    },
                    Appearance = new AppearanceSettingsDto
                    {
                        IsDarkMode = model.Appearance.IsDarkMode
                    }
                };

                var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // ignore save failures
            }
        }

        private static BrowserSettingsDto? ReadBrowserDto(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BrowserSettingsDto>(json);
            }
            catch
            {
                return null;
            }
        }

        private static RunnerLogSettingsDto? ReadRunnerDto(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<RunnerLogSettingsDto>(json);
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyBrowser(BrowserSettingsModel model, BrowserSettingsDto? dto)
        {
            if (dto == null)
            {
                return;
            }

            model.DisableCaching = dto.DisableCaching;
            model.BypassServiceWorker = dto.BypassServiceWorker;
            model.OpenDevToolsOnActivate = dto.OpenDevToolsOnActivate;
        }

        private static void ApplyRunner(RunnerLogSettingsModel model, RunnerLogSettingsDto? dto)
        {
            if (dto == null)
            {
                return;
            }

            model.Level = dto.Level;
            model.ApplyCategories(dto.Categories);
        }

        private static void ApplyRunnerSettings(RunnerSettingsModel model, RunnerSettingsDto? dto)
        {
            if (dto == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.WriteMode))
            {
                model.WriteMode = dto.WriteMode;
            }
        }

        private static void CopyBrowser(BrowserSettingsModel source, BrowserSettingsModel target)
        {
            target.DisableCaching = source.DisableCaching;
            target.BypassServiceWorker = source.BypassServiceWorker;
            target.OpenDevToolsOnActivate = source.OpenDevToolsOnActivate;
        }

        private static void CopyRunner(RunnerLogSettingsModel source, RunnerLogSettingsModel target)
        {
            target.Level = source.Level;
            target.ApplyCategories(source.ToCategories());
        }

        private static void CopyRunnerSettings(RunnerSettingsModel source, RunnerSettingsModel target)
        {
            target.WriteMode = source.WriteMode;
        }

        private static void ApplyAppearance(AppearanceSettingsModel model, AppearanceSettingsDto? dto)
        {
            if (dto == null)
            {
                return;
            }

            model.IsDarkMode = dto.IsDarkMode;
        }

        private static void CleanupLegacyFiles()
        {
            TryDelete(LegacyBrowserPath);
            TryDelete(LegacyRunnerPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
        }

        private sealed class AppSettingsDto
        {
            public BrowserSettingsDto Browser { get; set; } = new BrowserSettingsDto();
            public RunnerLogSettingsDto RunnerLog { get; set; } = new RunnerLogSettingsDto();
            public RunnerSettingsDto Runner { get; set; } = new RunnerSettingsDto();
            public AppearanceSettingsDto Appearance { get; set; } = new AppearanceSettingsDto();
        }

        private sealed class BrowserSettingsDto
        {
            public bool DisableCaching { get; set; } = true;
            public bool BypassServiceWorker { get; set; } = true;
            public bool OpenDevToolsOnActivate { get; set; }
        }

        private sealed class RunnerLogSettingsDto
        {
            public RunnerLogLevel Level { get; set; } = RunnerLogLevel.Info;
            public RunnerLogCategory Categories { get; set; } = RunnerLogCategory.All;
        }

        private sealed class RunnerSettingsDto
        {
            public string WriteMode { get; set; } = "FakeWrites";
        }

        private sealed class AppearanceSettingsDto
        {
            public bool IsDarkMode { get; set; } = true;
        }
    }
}
