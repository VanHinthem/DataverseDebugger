namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Root model for application-wide settings.
    /// </summary>
    /// <remarks>
    /// Contains settings for browser behavior, runner logging, runner execution, and UI appearance.
    /// </remarks>
    public sealed class AppSettingsModel
    {
        /// <summary>Gets the browser-related settings.</summary>
        public BrowserSettingsModel Browser { get; } = new BrowserSettingsModel();

        /// <summary>Gets the runner log settings.</summary>
        public RunnerLogSettingsModel RunnerLog { get; } = new RunnerLogSettingsModel();

        /// <summary>Gets the runner execution settings.</summary>
        public RunnerSettingsModel Runner { get; } = new RunnerSettingsModel();

        /// <summary>Gets the appearance settings.</summary>
        public AppearanceSettingsModel Appearance { get; } = new AppearanceSettingsModel();
    }
}
