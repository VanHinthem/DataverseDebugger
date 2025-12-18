using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// Dialog for searching and selecting a user for impersonation.
    /// </summary>
    /// <remarks>
    /// Allows searching Dataverse users by name or email, and returns the
    /// selected user for impersonation purposes. The MSCRMCallerID header
    /// will be injected into Web API requests when impersonation is active.
    /// </remarks>
    public partial class UserSearchDialog : Window
    {
        private readonly EnvironmentProfile _profile;
        private readonly string _accessToken;
        private DataverseUser? _selectedUser;

        /// <summary>
        /// Gets the user selected for impersonation, or null if none selected.
        /// </summary>
        public DataverseUser? SelectedUser => _selectedUser;

        /// <summary>
        /// Gets or sets whether impersonation was cleared (user selected "Clear Impersonation").
        /// </summary>
        public bool ImpersonationCleared { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserSearchDialog"/> class.
        /// </summary>
        /// <param name="profile">The environment profile with org URL.</param>
        /// <param name="accessToken">The access token for authentication.</param>
        /// <param name="currentImpersonatedUser">The currently impersonated user, if any.</param>
        public UserSearchDialog(EnvironmentProfile profile, string accessToken, DataverseUser? currentImpersonatedUser = null)
        {
            _profile = profile;
            _accessToken = accessToken;
            _selectedUser = currentImpersonatedUser;

            InitializeComponent();

            // Enable clear button if there's a current impersonation
            ClearImpersonationButton.IsEnabled = currentImpersonatedUser != null;

            // Load initial results
            Loaded += async (s, e) =>
            {
                SearchBox.Focus();
                await SearchUsersAsync(string.Empty);
            };
        }

        /// <summary>
        /// Handles Enter key press in the search box.
        /// </summary>
        private async void SearchBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SearchUsersAsync(SearchBox.Text);
            }
        }

        /// <summary>
        /// Handles the Search button click.
        /// </summary>
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await SearchUsersAsync(SearchBox.Text);
        }

        /// <summary>
        /// Searches for users matching the search text.
        /// </summary>
        /// <param name="searchText">The text to search for.</param>
        private async System.Threading.Tasks.Task SearchUsersAsync(string searchText)
        {
            UserListBox.ItemsSource = null;
            NoResultsText.Visibility = Visibility.Collapsed;
            LoadingText.Visibility = Visibility.Visible;

            try
            {
                // Run on background thread to keep UI responsive
                var users = await System.Threading.Tasks.Task.Run(async () => 
                    await UserSearchService.SearchUsersAsync(_profile, _accessToken, searchText));

                // Back on UI thread
                LoadingText.Visibility = Visibility.Collapsed;

                if (users.Count == 0)
                {
                    NoResultsText.Visibility = Visibility.Visible;
                }
                else
                {
                    UserListBox.ItemsSource = users;
                }
            }
            catch (Exception ex)
            {
                LoadingText.Visibility = Visibility.Collapsed;
                NoResultsText.Text = $"Error: {ex.Message}";
                NoResultsText.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Handles selection change in the user list.
        /// </summary>
        private void UserListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectButton.IsEnabled = UserListBox.SelectedItem != null;
        }

        /// <summary>
        /// Handles double-click on a user item.
        /// </summary>
        private void UserListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UserListBox.SelectedItem is DataverseUser user)
            {
                SelectUser(user);
            }
        }

        /// <summary>
        /// Handles the Select User button click.
        /// </summary>
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (UserListBox.SelectedItem is DataverseUser user)
            {
                SelectUser(user);
            }
        }

        /// <summary>
        /// Selects the specified user and closes the dialog.
        /// </summary>
        /// <param name="user">The user to select.</param>
        private void SelectUser(DataverseUser user)
        {
            _selectedUser = user;
            ImpersonationCleared = false;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Clear Impersonation button click.
        /// </summary>
        private void ClearImpersonationButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedUser = null;
            ImpersonationCleared = true;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Converts a string to visibility (Visible if not empty, Collapsed if empty).
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a string to visibility.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
