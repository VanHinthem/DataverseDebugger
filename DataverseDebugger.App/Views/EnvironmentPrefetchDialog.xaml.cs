using System;
using System.Threading.Tasks;
using System.Windows;
using DataverseDebugger.App.Auth;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// Dialog that pre-downloads metadata and the plugin catalog for a new environment.
    /// </summary>
    public partial class EnvironmentPrefetchDialog : Window
    {
        private readonly EnvironmentProfile _profile;

        public EnvironmentPrefetchDialog(EnvironmentProfile profile)
        {
            InitializeComponent();
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            var envName = string.IsNullOrWhiteSpace(_profile.Name) ? "environment" : _profile.Name;
            Title = $"Prepare {envName}";
            HeaderText.Text = $"Prepare {envName}";
            DescriptionText.Text = $"Download metadata and the plugin catalog for {envName} now so activation finishes faster.";
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await StartPrefetchAsync();
        }

        private async Task StartPrefetchAsync()
        {
            SetProgressActive(true);
            try
            {
                UpdateStatus("Signing in...");
                var auth = await AuthService.AcquireTokenInteractiveAsync(_profile);
                if (auth == null)
                {
                    throw new InvalidOperationException("Sign-in failed or was canceled.");
                }

                _profile.LastAccessToken = auth.AccessToken;
                _profile.SignedInUser = auth.User;
                _profile.AccessTokenExpiresOn = auth.ExpiresOn;

                UpdateStatus("Downloading metadata...");
                var metadata = await MetadataCacheService.RefreshMetadataAsync(_profile, auth.AccessToken);
                _profile.MetadataFetchedOn = metadata.LastUpdatedUtc;

                UpdateStatus("Downloading plugin catalog...");
                var catalog = await PluginCatalogService.RefreshCatalogAsync(_profile, auth.AccessToken);
                _profile.PluginCatalogFetchedOn = catalog.FetchedOnUtc;

                UpdateStatus("Ready!");
                DialogResult = true;
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Sign-in canceled.";
                ShowCloseButton();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Setup failed: {ex.Message}";
                ShowCloseButton();
            }
            finally
            {
                if (!IsVisible || DialogResult != true)
                {
                    SetProgressActive(false);
                }
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        private void ShowCloseButton()
        {
            CloseButton.Visibility = Visibility.Visible;
            CloseButton.IsEnabled = true;
            SetProgressActive(false);
        }

        private void SetProgressActive(bool active)
        {
            if (ProgressBar == null)
            {
                return;
            }

            ProgressBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            ProgressBar.IsIndeterminate = true;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
