using System.Windows.Controls;
using DataverseDebugger.App.Models;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View for editing application settings.
    /// </summary>
    /// <remarks>
    /// Provides UI for browser settings, runner log settings, runner execution settings,
    /// and appearance settings (theme toggle).
    /// </remarks>
    public partial class SettingsView : UserControl
    {
        /// <summary>
        /// Initializes a new instance with default settings.
        /// </summary>
        public SettingsView()
            : this(new BrowserSettingsModel(), new RunnerLogSettingsModel(), new RunnerSettingsModel(), new AppearanceSettingsModel())
        {
        }

        /// <summary>
        /// Initializes a new instance with the provided settings models.
        /// </summary>
        /// <param name="browserSettings">Browser-related settings.</param>
        /// <param name="runnerLogSettings">Runner logging settings.</param>
        /// <param name="runnerSettings">Runner execution settings.</param>
        /// <param name="appearanceSettings">UI appearance settings.</param>
        public SettingsView(
            BrowserSettingsModel browserSettings,
            RunnerLogSettingsModel runnerLogSettings,
            RunnerSettingsModel runnerSettings,
            AppearanceSettingsModel appearanceSettings)
        {
            InitializeComponent();
            DataContext = new SettingsViewModel(browserSettings, runnerLogSettings, runnerSettings, appearanceSettings);
        }
    }
}
