using System.Collections.Generic;
using System.Windows;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// Dialog for selecting a Visual Studio instance to attach for debugging.
    /// </summary>
    public partial class DebuggerAttachDialog : Window
    {
        /// <summary>Gets the selected Visual Studio instance.</summary>
        public VisualStudioInstance? SelectedInstance { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DebuggerAttachDialog"/> class.
        /// </summary>
        /// <param name="instances">Available Visual Studio instances.</param>
        public DebuggerAttachDialog(IReadOnlyList<VisualStudioInstance> instances)
        {
            InitializeComponent();
            InstancesList.ItemsSource = instances;
            if (instances != null && instances.Count > 0)
            {
                InstancesList.SelectedIndex = 0;
            }
        }

        private void OnAttachClick(object sender, RoutedEventArgs e)
        {
            SelectedInstance = InstancesList.SelectedItem as VisualStudioInstance;
            if (SelectedInstance == null)
            {
                MessageBox.Show("Select a Visual Studio instance to attach.", "Attach Debugger", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }

        private void OnInstanceDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OnAttachClick(sender, new RoutedEventArgs());
        }
    }
}
