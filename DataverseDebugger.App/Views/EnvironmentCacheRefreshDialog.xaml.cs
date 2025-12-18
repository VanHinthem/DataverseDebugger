using System;
using System.Threading.Tasks;
using System.Windows;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// Dialog that refreshes cached metadata and/or plugin catalog for an environment.
    /// </summary>
    public partial class EnvironmentCacheRefreshDialog : Window
    {
        private readonly EnvironmentProfile _profile;
        private readonly string _accessToken;
        private readonly bool _refreshMetadata;
        private readonly bool _refreshCatalog;

        public MetadataCacheResult? MetadataResult { get; private set; }
        public PluginCatalog? CatalogResult { get; private set; }

        public EnvironmentCacheRefreshDialog(EnvironmentProfile profile, string accessToken, bool refreshMetadata, bool refreshCatalog)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("Access token is required.", nameof(accessToken));
            }

            if (!refreshMetadata && !refreshCatalog)
            {
                throw new ArgumentException("At least one refresh target must be specified.", nameof(refreshMetadata));
            }

            InitializeComponent();

            _profile = profile;
            _accessToken = accessToken;
            _refreshMetadata = refreshMetadata;
            _refreshCatalog = refreshCatalog;

            var envName = string.IsNullOrWhiteSpace(profile.Name) ? "environment" : profile.Name;
            if (refreshMetadata && refreshCatalog)
            {
                HeaderText.Text = $"Refresh metadata and catalog";
                DescriptionText.Text = $"Refreshing metadata and plugin catalog for {envName}.";
            }
            else if (refreshMetadata)
            {
                HeaderText.Text = "Refresh metadata";
                DescriptionText.Text = $"Refreshing cached metadata for {envName}.";
            }
            else
            {
                HeaderText.Text = "Refresh plugin catalog";
                DescriptionText.Text = $"Refreshing the plugin catalog for {envName}.";
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await StartRefreshAsync();
        }

        private async Task StartRefreshAsync()
        {
            SetProgressActive(true);
            try
            {
                if (_refreshMetadata)
                {
                    UpdateStatus("Refreshing metadata...");
                    MetadataResult = await MetadataCacheService.RefreshMetadataAsync(_profile, _accessToken);
                    _profile.MetadataFetchedOn = MetadataResult.LastUpdatedUtc;
                }

                if (_refreshCatalog)
                {
                    UpdateStatus("Refreshing plugin catalog...");
                    CatalogResult = await PluginCatalogService.RefreshCatalogAsync(_profile, _accessToken);
                    _profile.PluginCatalogFetchedOn = CatalogResult.FetchedOnUtc;
                }

                UpdateStatus("Finished.");
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Refresh failed: {ex.Message}";
                ShowCloseButton();
            }
            finally
            {
                if (DialogResult == true)
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
            ProgressBar.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            ProgressBar.IsIndeterminate = active;
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
