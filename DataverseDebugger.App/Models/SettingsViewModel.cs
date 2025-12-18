namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// View model for the settings view, aggregating all settings models.
    /// </summary>
    public sealed class SettingsViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
        /// </summary>
        /// <param name="browser">The browser settings model.</param>
        /// <param name="runnerLog">The runner log settings model.</param>
        /// <param name="runner">The runner settings model.</param>
        /// <param name="appearance">The appearance settings model.</param>
        public SettingsViewModel(
            BrowserSettingsModel browser,
            RunnerLogSettingsModel runnerLog,
            RunnerSettingsModel runner,
            AppearanceSettingsModel appearance)
        {
            Browser = browser;
            RunnerLog = runnerLog;
            Runner = runner;
            Appearance = appearance;
        }

        /// <summary>Gets the browser settings.</summary>
        public BrowserSettingsModel Browser { get; }

        /// <summary>Gets the runner log settings.</summary>
        public RunnerLogSettingsModel RunnerLog { get; }

        /// <summary>Gets the runner execution settings.</summary>
        public RunnerSettingsModel Runner { get; }

        /// <summary>Gets the appearance settings.</summary>
        public AppearanceSettingsModel Appearance { get; }
    }
}
