using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DataverseDebugger.App.Auth;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// Dialog that signs the user in and lists their Dataverse environments.
    /// </summary>
    public partial class EnvironmentDiscoveryDialog : Window
    {
        private const string DiscoveryCacheEnvName = "__globaldisco";
        private readonly ObservableCollection<DataverseInstance> _instances = new();
        private readonly ICollectionView? _instancesView;
        private AuthResultInfo? _discoveryAuth;
        private string? _discoveryCachePath;
        private string _urlFilter = string.Empty;
        private bool _instancesLoaded;

        /// <summary>Gets the profile created from the selected environment.</summary>
        public EnvironmentProfile? CreatedProfile { get; private set; }

        public EnvironmentDiscoveryDialog()
        {
            InitializeComponent();
            InstanceList.ItemsSource = _instances;
            _instancesView = CollectionViewSource.GetDefaultView(_instances);
            if (_instancesView != null)
            {
                _instancesView.Filter = FilterInstance;
            }
            _discoveryCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(DiscoveryCacheEnvName, "token-cache");
            RefreshSignInOptions();
            UpdateInstanceListState();
            UpdateAddButtonContent();
            UpdateAuthIndicator();
        }

        private async void OnSignInClick(object sender, RoutedEventArgs e)
        {
            await BeginDiscoveryAsync(silentOnly: false, forceNewSignIn: true);
        }

        private async void OnUseCachedSignInClick(object sender, RoutedEventArgs e)
        {
            await BeginDiscoveryAsync(silentOnly: true, forceNewSignIn: false);
        }

        private void OnInstanceSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (InstanceList.SelectedItem is not DataverseInstance instance)
            {
                SelectedUrlText.Text = string.Empty;
                ManualUrlBox.Text = string.Empty;
                SelectedUrlText.Visibility = Visibility.Collapsed;
                UpdateAddButtonContent();
                return;
            }

            DisplayNameBox.Text = instance.FriendlyName;
            var selectedUrl = instance.WebUrl ?? string.Empty;
            ManualUrlBox.Text = selectedUrl;
            SelectedUrlText.Text = selectedUrl;
            SelectedUrlText.Visibility = Visibility.Visible;
            UpdateAddButtonContent();
        }

        private async Task BeginDiscoveryAsync(bool silentOnly, bool forceNewSignIn)
        {
            if (forceNewSignIn)
            {
                ClearDiscoveryCache();
                RefreshSignInOptions();
            }

            _discoveryAuth = null;
            UpdateAuthIndicator();
            ResetInstanceList();

            await RunAsync(async () =>
            {
                StatusText.Text = silentOnly ? "Using saved sign-in..." : "Signing in...";
                _discoveryAuth = await AuthService.AcquireDiscoveryTokenAsync(silentOnly);
                if (_discoveryAuth == null)
                {
                    StatusText.Text = silentOnly
                        ? "Saved sign-in expired. Choose New sign in."
                        : "Sign-in failed. Try again.";
                    return;
                }

                StatusText.Text = "Signed in. Enter the environment URL or choose Select environment to load your list.";
            });

            RefreshSignInOptions();
            UpdateAuthIndicator();
        }

        private async Task LoadInstancesAsync()
        {
            if (_discoveryAuth == null)
            {
                return;
            }

            StatusText.Text = "Fetching environments...";
            var instances = await DiscoveryService.GetInstancesAsync(_discoveryAuth.AccessToken);
            _instances.Clear();
            foreach (var instance in instances)
            {
                _instances.Add(instance);
            }

            _instancesLoaded = true;
            _instancesView?.Refresh();
            UpdateInstanceListState();
        }

        private async void OnAddEnvironmentClick(object sender, RoutedEventArgs e)
        {
            var name = DisplayNameBox.Text?.Trim() ?? string.Empty;

            if (IsManualEntryReady())
            {
                await AddEnvironmentFromValues(name, GetManualUrlEntry(), "manual-url");
                return;
            }

            if (!_instancesLoaded)
            {
                if (_discoveryAuth == null)
                {
                    MessageBox.Show("Sign in first, then choose Select environment to load your environments.", "Select environment", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                AddButton.IsEnabled = false;
                await RunAsync(async () =>
                {
                    StatusText.Text = "Loading environments...";
                    await LoadInstancesAsync();
                    if (_instancesLoaded && _instances.Count > 0)
                    {
                        StatusText.Text = "Select the environment to add, then choose Select environment again.";
                    }
                    else if (_instancesLoaded)
                    {
                        StatusText.Text = "No environments were returned for this account.";
                    }
                }, disableSignInButtons: false);
                AddButton.IsEnabled = true;
                return;
            }

            if (InstanceList.SelectedItem is not DataverseInstance instance)
            {
                MessageBox.Show("Select an environment from the list.", "Select environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Enter a display name for this environment.", "Select environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(instance.WebUrl))
            {
                MessageBox.Show("The selected environment does not include a web URL. Try another environment or reload the list.", "Select environment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await AddEnvironmentFromValues(name, instance.WebUrl, instance.UniqueName);
        }

        private bool HasDiscoveryCache()
        {
            if (string.IsNullOrWhiteSpace(_discoveryCachePath))
            {
                return false;
            }

            try
            {
                return Directory.Exists(_discoveryCachePath) &&
                       Directory.EnumerateFiles(_discoveryCachePath, "*", SearchOption.TopDirectoryOnly).Any();
            }
            catch
            {
                return false;
            }
        }

        private void ClearDiscoveryCache()
        {
            if (string.IsNullOrWhiteSpace(_discoveryCachePath))
            {
                return;
            }

            try
            {
                if (Directory.Exists(_discoveryCachePath))
                {
                    Directory.Delete(_discoveryCachePath, recursive: true);
                }
            }
            catch
            {
                // ignore cache cleanup failures; user can still sign in again
            }
        }

        private void RefreshSignInOptions()
        {
            var hasCache = HasDiscoveryCache();
            if (UseCachedSignInButton != null)
            {
                UseCachedSignInButton.Visibility = hasCache ? Visibility.Visible : Visibility.Collapsed;
                UseCachedSignInButton.IsEnabled = hasCache && SignInButton.IsEnabled;
            }

            if (SignInButton != null)
            {
                SignInButton.Content = hasCache ? "🔐 New sign in" : "🔐 Sign in";
            }
        }

        private void SetSignInButtonsEnabled(bool enabled)
        {
            if (SignInButton != null)
            {
                SignInButton.IsEnabled = enabled;
            }

            if (UseCachedSignInButton != null)
            {
                UseCachedSignInButton.IsEnabled = enabled && HasDiscoveryCache();
            }

            RefreshSignInOptions();
        }

        private void OnFilterTextChanged(object sender, TextChangedEventArgs e)
        {
            _urlFilter = UrlFilterBox?.Text?.Trim().TrimEnd('/') ?? string.Empty;
            _instancesView?.Refresh();
            UpdateInstanceListState();
        }

        private bool FilterInstance(object obj)
        {
            if (obj is not DataverseInstance instance)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(_urlFilter))
            {
                return true;
            }

            var url = instance.WebUrl ?? string.Empty;
            return url.IndexOf(_urlFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateInstanceListState()
        {
            var hasAny = _instances.Count > 0;
            var hasVisible = _instancesView != null && !_instancesView.IsEmpty;
            var listReady = _instancesLoaded && hasAny;

            if (FilterPanel != null)
            {
                FilterPanel.Visibility = listReady ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!listReady)
            {
                InstanceList.Visibility = Visibility.Collapsed;
                DetailsPanel.Visibility = Visibility.Visible;
                SelectedUrlText.Text = string.Empty;
                SelectedUrlText.Visibility = Visibility.Collapsed;
                if (!_instancesLoaded)
                {
                    StatusText.Text = _discoveryAuth == null
                        ? "Sign in, then enter the environment URL or choose Select environment to load the list."
                        : "Enter the environment URL or choose Select environment to load your environments.";
                }
                else
                {
                    StatusText.Text = "No environments were returned. Enter the environment URL or try loading again.";
                }
                UpdateAddButtonContent();
                return;
            }

            if (!hasVisible)
            {
                InstanceList.Visibility = Visibility.Collapsed;
                DetailsPanel.Visibility = Visibility.Visible;
                SelectedUrlText.Text = string.Empty;
                SelectedUrlText.Visibility = Visibility.Collapsed;
                StatusText.Text = "No environments match this URL filter.";
                InstanceList.SelectedItem = null;
                UpdateAddButtonContent();
                return;
            }

            InstanceList.Visibility = Visibility.Visible;
            DetailsPanel.Visibility = Visibility.Visible;

            var selected = InstanceList.SelectedItem as DataverseInstance;
            if (selected == null || (_instancesView != null && !_instancesView.Contains(selected)))
            {
                InstanceList.SelectedIndex = 0;
                selected = InstanceList.SelectedItem as DataverseInstance;
            }

            if (selected != null)
            {
                SelectedUrlText.Text = selected.WebUrl ?? string.Empty;
                SelectedUrlText.Visibility = Visibility.Visible;
            }
            else
            {
                SelectedUrlText.Text = string.Empty;
                SelectedUrlText.Visibility = Visibility.Collapsed;
            }

            StatusText.Text = "Select the environment to add, then choose Select environment.";

            UpdateAddButtonContent();
        }

        private string GetManualUrlEntry()
        {
            return ManualUrlBox?.Text?.Trim().TrimEnd('/') ?? string.Empty;
        }

        private bool HasValidManualUrl()
        {
            return IsValidDataverseUrl(GetManualUrlEntry());
        }

        private bool HasDisplayName()
        {
            return !string.IsNullOrWhiteSpace(DisplayNameBox?.Text?.Trim());
        }

        private bool IsManualEntryReady()
        {
            return HasDisplayName() && HasValidManualUrl();
        }

        private static bool IsValidDataverseUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        private void OnManualUrlTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAddButtonContent();
        }

        private void OnDisplayNameTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAddButtonContent();
        }

        private void UpdateAddButtonContent()
        {
            if (AddButton == null)
            {
                return;
            }

            AddButton.Content = IsManualEntryReady() ? "Add environment" : "Select environment";
        }

        private void UpdateAuthIndicator()
        {
            if (AuthStatusText == null)
            {
                return;
            }

            if (_discoveryAuth == null)
            {
                AuthStatusText.Text = "Not signed in";
                AuthStatusText.Foreground = Brushes.Firebrick;
                return;
            }

            var userName = _discoveryAuth.User;
            AuthStatusText.Text = string.IsNullOrWhiteSpace(userName)
                ? "Signed in"
                : $"Signed in as {userName}";
            AuthStatusText.Foreground = Brushes.ForestGreen;
        }

        private async Task RunAsync(Func<Task> action, bool disableSignInButtons = true)
        {
            if (disableSignInButtons)
            {
                SetSignInButtonsEnabled(false);
            }
            ProgressBar.Visibility = Visibility.Visible;
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Sign-in canceled.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Operation failed.";
                MessageBox.Show(ex.Message, "Add environment", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ProgressBar.Visibility = Visibility.Collapsed;
                if (disableSignInButtons)
                {
                    SetSignInButtonsEnabled(true);
                }
            }
        }

        private async Task AddEnvironmentFromValues(string name, string orgUrl, string? notes)
        {
            var trimmedName = name?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                MessageBox.Show("Enter a display name for this environment.", "Add environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var normalizedUrl = orgUrl?.Trim().TrimEnd('/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedUrl) || !IsValidDataverseUrl(normalizedUrl))
            {
                MessageBox.Show("Enter a valid environment URL.", "Add environment", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_discoveryAuth == null)
            {
                MessageBox.Show("Sign in first.", "Add environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var safeName = trimmedName!;

            var profile = new EnvironmentProfile
            {
                Name = safeName,
                OrgUrl = normalizedUrl,
                CaptureApiOnly = true,
                CaptureAutoProxy = true,
                CaptureNavigateUrl = normalizedUrl,
                Notes = notes ?? string.Empty,
                TenantId = _discoveryAuth.TenantId ?? "organizations",
                ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d",
                WebViewCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(safeName, "webview-cache"),
                TokenCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(safeName, "token-cache")
            };

            if (!string.IsNullOrWhiteSpace(_discoveryCachePath))
            {
                AuthService.CloneTokenCache(_discoveryCachePath, profile.TokenCachePath);
            }

            AddButton.IsEnabled = false;
            await RunAsync(async () =>
            {
                StatusText.Text = "Finalizing sign-in...";
                var auth = await AuthService.AcquireTokenInteractiveAsync(profile);
                if (auth != null)
                {
                    if (!string.IsNullOrEmpty(auth.AccessToken))
                    {
                        profile.LastAccessToken = auth.AccessToken;
                    }

                    if (!string.IsNullOrEmpty(auth.User))
                    {
                        profile.SignedInUser = auth.User;
                    }

                    profile.AccessTokenExpiresOn = auth.ExpiresOn;
                }

                CreatedProfile = profile;
                DialogResult = true;
            }, disableSignInButtons: false);
            AddButton.IsEnabled = true;
        }

        private void ResetInstanceList()
        {
            _instances.Clear();
            _instancesLoaded = false;
            InstanceList.SelectedItem = null;
            _instancesView?.Refresh();
            SelectedUrlText.Text = string.Empty;
            SelectedUrlText.Visibility = Visibility.Collapsed;
            UpdateInstanceListState();
        }
    }
}
