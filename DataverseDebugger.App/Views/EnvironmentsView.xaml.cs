using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Auth;
using Microsoft.Win32;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View for managing Dataverse environment profiles.
    /// </summary>
    /// <remarks>
    /// Allows adding, editing, and removing environments, configuring authentication,
    /// selecting plugin assemblies, and managing plugin catalogs and metadata cache.
    /// </remarks>
    public partial class EnvironmentsView : UserControl
    {
        private const string DefaultClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
        private const string DefaultTenantId = "organizations";
        private readonly string _storePath = Path.Combine(
            AppContext.BaseDirectory,
            "settings",
            "environments.json");

        public ObservableCollection<EnvironmentProfile> Profiles { get; } = new ObservableCollection<EnvironmentProfile>();
        private EnvironmentProfile? _selected;
        private readonly Func<EnvironmentProfile, Task> _onActivate;
        private readonly Action? _onOpenBrowser;
        private readonly Func<EnvironmentProfile, Task>? _onDeactivate;

        public EnvironmentsView(Func<EnvironmentProfile, Task> onActivate, Action? onOpenBrowser = null, Func<EnvironmentProfile, Task>? onDeactivate = null)
        {
            InitializeComponent();
            _onActivate = onActivate;
            _onOpenBrowser = onOpenBrowser;
            _onDeactivate = onDeactivate;
            DataContext = this;
            LoadProfiles();
            var numbersUpdated = EnsureEnvironmentNumbers();
            var cachePathsUpdated = false;
            foreach (var profile in Profiles)
            {
                cachePathsUpdated |= RefreshCachePaths(profile);
            }
            if (numbersUpdated || cachePathsUpdated)
            {
                SaveProfiles();
            }
            _selected = Profiles.FirstOrDefault(p => p.IsActive) ?? Profiles.FirstOrDefault();
            if (_selected != null)
            {
                EnvList.SelectedItem = _selected;
                BindToForm(_selected);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = EnvList.SelectedItem as EnvironmentProfile;
            BindToForm(_selected);
        }

        private void OnAddClick(object sender, RoutedEventArgs e)
        {
            var dialog = new EnvironmentDiscoveryDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.CreatedProfile != null)
            {
                _selected = dialog.CreatedProfile;
                EnsureEnvironmentNumber(_selected);
                _ = RefreshCachePaths(_selected);
                Profiles.Add(_selected);
                EnvList.SelectedItem = _selected;
                BindToForm(_selected);
                SaveProfiles();
                PromptPrefetch(_selected);
            }
        }

        private void PromptPrefetch(EnvironmentProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            var owner = Window.GetWindow(this) ?? Application.Current?.MainWindow;
            var promptText = "Download metadata and the plugin catalog now so this environment is ready the next time you activate it?";
            var result = owner != null
                ? MessageBox.Show(owner, promptText, "Prepare environment", MessageBoxButton.YesNo, MessageBoxImage.Question)
                : MessageBox.Show(promptText, "Prepare environment", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            var prefetchDialog = new EnvironmentPrefetchDialog(profile)
            {
                Owner = owner
            };

            var success = prefetchDialog.ShowDialog() == true;
            if (success)
            {
                RefreshMetadataStatus(profile);
                RefreshCatalogStatus(profile);
                SaveProfiles();
                var successText = "Metadata and plugin catalog downloaded. You can still refresh them later from the environment details if needed.";
                if (owner != null)
                {
                    MessageBox.Show(owner, successText, "Environment prepared", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(successText, "Environment prepared", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select or add an environment first.", "Save environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateFromForm(_selected);
            EnvList.Items.Refresh();
            SaveProfiles();
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            EnvironmentPathService.DeleteEnvironmentCache(_selected);
            Profiles.Remove(_selected);
            _selected = null;
            ClearForm();
            SaveProfiles();
        }

        private async void OnActivateClick(object sender, RoutedEventArgs e)
        {
            await ActivateSelectedAsync(openIfActive: false);
        }

        private async void OnDeactivateClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select an environment to deactivate.", "Deactivate environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_selected.IsActive)
            {
                MessageBox.Show("The selected environment is not active.", "Deactivate environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (_onDeactivate != null)
                {
                    await _onDeactivate(_selected);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deactivation failed: {ex.Message}", "Deactivate environment", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            foreach (var profile in Profiles)
            {
                profile.IsActive = false;
            }
            EnvList.Items.Refresh();
            SaveProfiles();
        }

        private async void OnEnvironmentDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            await ActivateSelectedAsync(openIfActive: true);
        }

        private async Task ActivateSelectedAsync(bool openIfActive)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select an environment to activate.", "Activate environment", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_selected.IsActive)
            {
                if (openIfActive)
                {
                    _onOpenBrowser?.Invoke();
                }
                return;
            }
            UpdateFromForm(_selected);
            MarkActive(_selected);
            SaveProfiles();
            await _onActivate(_selected);
        }

        private void OnAddAssemblyClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Assemblies (*.dll)|*.dll|All files (*.*)|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                var list = AssemblyList.Items.Cast<string>().ToList();
                list.AddRange(dlg.FileNames);
                AssemblyList.ItemsSource = list;
            }
        }

        private void OnRemoveAssemblyClick(object sender, RoutedEventArgs e)
        {
            if (AssemblyList.SelectedItem is string asm)
            {
                var list = AssemblyList.Items.Cast<string>().ToList();
                list.Remove(asm);
                AssemblyList.ItemsSource = list;
            }
        }

        private void LoadProfiles()
        {
            try
            {
                if (!File.Exists(_storePath))
                {
                    return;
                }
                var json = File.ReadAllText(_storePath);
                var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<EnvironmentProfile>>(json);
                if (list == null) return;
                Profiles.Clear();
                foreach (var p in list)
                {
                    p.ClientId = DefaultClientId;
                    p.TenantId = DefaultTenantId;
                    Profiles.Add(p);
                }

            }
            catch
            {
                // ignore load failures
            }
        }

        private void SaveProfiles()
        {
            try
            {
                var dir = Path.GetDirectoryName(_storePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = System.Text.Json.JsonSerializer.Serialize(Profiles, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storePath, json);
            }
            catch
            {
                // ignore save failures
            }
        }

        private void BindToForm(EnvironmentProfile? profile)
        {
            if (profile == null)
            {
                ClearForm();
                return;
            }

            NameBox.Text = profile.Name;
            OrgUrlBox.Text = profile.OrgUrl;
            NotesBox.Text = profile.Notes;
            AssemblyList.ItemsSource = profile.PluginAssemblies.ToList();
            TraceVerboseCheck.IsChecked = profile.TraceVerbose;
            ApiOnlyCheck.IsChecked = profile.CaptureApiOnly;
            NavigateUrlBox.Text = profile.CaptureNavigateUrl ?? string.Empty;
            TokenCachePathText.Text = profile.TokenCachePath ?? string.Empty;
            WebViewCachePathText.Text = profile.WebViewCachePath ?? string.Empty;
            UpdateAuthStatus(profile);
            UpdateMetadataStatus(profile);
            UpdateWebViewCacheStatus(profile);
            UpdateCatalogStatus(profile);
        }

        private void ClearForm()
        {
            NameBox.Text = string.Empty;
            OrgUrlBox.Text = string.Empty;
            NotesBox.Text = string.Empty;
            AssemblyList.ItemsSource = null;
            TraceVerboseCheck.IsChecked = false;
            ApiOnlyCheck.IsChecked = true;
            NavigateUrlBox.Text = string.Empty;
            TokenCachePathText.Text = string.Empty;
            WebViewCachePathText.Text = string.Empty;
            AuthStatusText.Text = string.Empty;
            MetadataPathText.Text = string.Empty;
            MetadataStatusText.Text = string.Empty;
            WebViewCachePathSummaryText.Text = string.Empty;
            WebViewCacheSizeText.Text = string.Empty;
            CatalogPathText.Text = string.Empty;
            CatalogStatusText.Text = string.Empty;
        }

        private void UpdateFromForm(EnvironmentProfile profile)
        {
            EnsureEnvironmentNumber(profile);
            profile.Name = NameBox.Text ?? string.Empty;
            profile.OrgUrl = OrgUrlBox.Text ?? string.Empty;
            profile.Notes = NotesBox.Text ?? string.Empty;
            profile.ClientId = DefaultClientId;
            profile.TenantId = DefaultTenantId;
            profile.PluginAssemblies = AssemblyList.Items.Cast<string>().ToList();
            profile.TraceVerbose = TraceVerboseCheck.IsChecked == true;
            profile.CaptureApiOnly = ApiOnlyCheck.IsChecked == true;
            profile.CaptureNavigateUrl = NavigateUrlBox.Text;
            profile.TokenCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(profile, "token-cache");
            profile.WebViewCachePath = EnvironmentPathService.EnsureEnvironmentSubfolder(profile, "webview-cache");
            TokenCachePathText.Text = profile.TokenCachePath;
            WebViewCachePathText.Text = profile.WebViewCachePath;
            UpdateAuthStatus(profile);
            UpdateWebViewCacheStatus(profile);
        }

        private async void OnSignInClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select an environment first.", "Sign in", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateFromForm(_selected);
            try
            {
                var result = await AuthService.AcquireTokenInteractiveAsync(_selected);
                if (result != null)
                {
                    _selected.SignedInUser = result.User;
                    _selected.AccessTokenExpiresOn = result.ExpiresOn;
                    _selected.LastAccessToken = result.AccessToken;
                    UpdateAuthStatus(_selected);
                    SaveProfiles();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign-in failed: {ex.Message}", "Sign in", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnSignOutClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            UpdateFromForm(_selected);
            try
            {
                await AuthService.SignOutAsync(_selected);
                _selected.SignedInUser = null;
                _selected.AccessTokenExpiresOn = null;
                _selected.LastAccessToken = null;
                UpdateAuthStatus(_selected);
                SaveProfiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign-out failed: {ex.Message}", "Sign out", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateAuthStatus(EnvironmentProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.SignedInUser))
            {
                var expiry = profile.AccessTokenExpiresOn?.ToString("g") ?? "unknown";
                AuthStatusText.Text = $"Signed in as {profile.SignedInUser}, expires {expiry}";
            }
            else
            {
                AuthStatusText.Text = "Not signed in";
            }
        }

        public void RefreshAuthStatus(EnvironmentProfile profile) => UpdateAuthStatus(profile);
        public void RefreshMetadataStatus(EnvironmentProfile profile) => UpdateMetadataStatus(profile);
        public void RefreshCatalogStatus(EnvironmentProfile profile) => UpdateCatalogStatus(profile);
        public void PersistProfiles() => SaveProfiles();

        public EnvironmentProfile? ActiveProfile => Profiles.FirstOrDefault(p => p.IsActive);

        public void ClearActiveProfiles()
        {
            foreach (var p in Profiles)
            {
                p.IsActive = false;
            }
            EnvList.SelectedItem = null;
            EnvList.Items.Refresh();
            SaveProfiles();
        }

        private void MarkActive(EnvironmentProfile active)
        {
            foreach (var p in Profiles)
            {
                p.IsActive = ReferenceEquals(p, active);
            }
            EnvList.Items.Refresh();
        }

        private void UpdateMetadataStatus(EnvironmentProfile profile)
        {
            var path = MetadataCacheService.GetMetadataPath(profile);
            MetadataPathText.Text = path;
            if (profile.MetadataFetchedOn.HasValue)
            {
                MetadataStatusText.Text = $"{profile.MetadataFetchedOn.Value:g} (UTC)";
            }
            else
            {
                var exists = System.IO.File.Exists(path);
                MetadataStatusText.Text = exists ? "Cached (timestamp unknown)" : "Not cached";
            }
        }

        private void UpdateWebViewCacheStatus(EnvironmentProfile profile)
        {
            var path = profile.WebViewCachePath ?? EnvironmentPathService.EnsureEnvironmentSubfolder(profile, "webview-cache");
            WebViewCachePathSummaryText.Text = path;
            WebViewCacheSizeText.Text = FormatSize(GetDirectorySize(path));
        }

        private void UpdateCatalogStatus(EnvironmentProfile profile)
        {
            var path = PluginCatalogService.GetCatalogPath(profile);
            CatalogPathText.Text = path;
            if (profile.PluginCatalogFetchedOn.HasValue)
            {
                CatalogStatusText.Text = $"{profile.PluginCatalogFetchedOn.Value:g} (UTC)";
            }
            else
            {
                var exists = System.IO.File.Exists(path);
                CatalogStatusText.Text = exists ? "Cached (timestamp unknown)" : "Not cached";
            }
        }

        private static long GetDirectorySize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return 0;
            }

            try
            {
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                    .Select(f => new FileInfo(f).Length)
                    .Sum();
            }
            catch
            {
                return 0;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            if (bytes < 1024) return $"{bytes} B";
            var kb = bytes / 1024d;
            if (kb < 1024) return $"{kb:F1} KB";
            var mb = kb / 1024d;
            if (mb < 1024) return $"{mb:F1} MB";
            var gb = mb / 1024d;
            return $"{gb:F1} GB";
        }

        private bool EnsureEnvironmentNumbers()
        {
            var used = new HashSet<int>();
            var max = 0;
            var updated = false;

            foreach (var profile in Profiles)
            {
                var parsed = ParseEnvironmentNumber(profile.EnvironmentNumber);
                if (parsed > 0 && !used.Contains(parsed))
                {
                    used.Add(parsed);
                    if (parsed > max)
                    {
                        max = parsed;
                    }

                    var formatted = FormatEnvironmentNumber(parsed);
                    if (!string.Equals(profile.EnvironmentNumber, formatted, StringComparison.Ordinal))
                    {
                        profile.EnvironmentNumber = formatted;
                        updated = true;
                    }
                    continue;
                }

                var next = max + 1;
                profile.EnvironmentNumber = FormatEnvironmentNumber(next);
                used.Add(next);
                max = next;
                updated = true;
            }

            return updated;
        }

        private void EnsureEnvironmentNumber(EnvironmentProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            var parsed = ParseEnvironmentNumber(profile.EnvironmentNumber);
            if (parsed > 0 && !Profiles.Any(p => !ReferenceEquals(p, profile) && ParseEnvironmentNumber(p.EnvironmentNumber) == parsed))
            {
                profile.EnvironmentNumber = FormatEnvironmentNumber(parsed);
                return;
            }

            profile.EnvironmentNumber = GetNextEnvironmentNumber();
        }

        private string GetNextEnvironmentNumber()
        {
            var max = Profiles
                .Select(p => ParseEnvironmentNumber(p.EnvironmentNumber))
                .DefaultIfEmpty(0)
                .Max();
            return FormatEnvironmentNumber(max + 1);
        }

        private static int ParseEnvironmentNumber(string? value)
        {
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : 0;
        }

        private static string FormatEnvironmentNumber(int number)
        {
            return number <= 0 ? string.Empty : number.ToString("D3");
        }

        private bool RefreshCachePaths(EnvironmentProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var root = EnvironmentPathService.GetEnvironmentCacheRoot(profile);
            var tokenPath = Path.Combine(root, "token-cache");
            var webPath = Path.Combine(root, "webview-cache");
            var changed = false;

            if (!string.Equals(profile.TokenCachePath, tokenPath, StringComparison.OrdinalIgnoreCase))
            {
                profile.TokenCachePath = tokenPath;
                changed = true;
            }

            if (!string.Equals(profile.WebViewCachePath, webPath, StringComparison.OrdinalIgnoreCase))
            {
                profile.WebViewCachePath = webPath;
                changed = true;
            }

            return changed;
        }

        private bool ShowCacheRefreshDialog(
            EnvironmentProfile profile,
            bool refreshMetadata,
            bool refreshCatalog,
            out MetadataCacheResult? metadataResult,
            out PluginCatalog? catalogResult)
        {
            metadataResult = null;
            catalogResult = null;

            if (profile == null || string.IsNullOrWhiteSpace(profile.LastAccessToken))
            {
                return false;
            }

            var dialog = new EnvironmentCacheRefreshDialog(profile, profile.LastAccessToken!, refreshMetadata, refreshCatalog)
            {
                Owner = Window.GetWindow(this) ?? Application.Current?.MainWindow
            };

            var result = dialog.ShowDialog();
            if (result == true)
            {
                metadataResult = dialog.MetadataResult;
                catalogResult = dialog.CatalogResult;
                return true;
            }

            return false;
        }

        private void OnRefreshMetadataClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select an environment first.", "Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateFromForm(_selected);
            if (string.IsNullOrWhiteSpace(_selected.LastAccessToken))
            {
                MessageBox.Show("Sign in or activate the environment to fetch metadata.", "Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!ShowCacheRefreshDialog(_selected, refreshMetadata: true, refreshCatalog: false, out var metadataResult, out _))
            {
                return;
            }

            UpdateMetadataStatus(_selected);
            SaveProfiles();
            var sizeText = FormatSize(metadataResult?.SizeBytes ?? 0);
            MessageBox.Show($"Metadata refreshed ({sizeText}).", "Metadata", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnClearWebViewCacheClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select an environment first.", "WebView cache", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateFromForm(_selected);
            var path = _selected.WebViewCachePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show("No WebView cache path is configured.", "WebView cache", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                Directory.CreateDirectory(path);
                UpdateWebViewCacheStatus(_selected);
                SaveProfiles();
                MessageBox.Show("WebView cache cleared.", "WebView cache", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView cache clear failed: {ex.Message}", "WebView cache", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnRefreshCatalogClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null)
            {
                MessageBox.Show("Select an environment first.", "Plugin catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateFromForm(_selected);
            if (string.IsNullOrWhiteSpace(_selected.LastAccessToken))
            {
                MessageBox.Show("Sign in or activate the environment to fetch plugin catalog.", "Plugin catalog", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!ShowCacheRefreshDialog(_selected, refreshMetadata: false, refreshCatalog: true, out _, out var catalogResult))
            {
                return;
            }

            UpdateCatalogStatus(_selected);
            SaveProfiles();
            var stepCount = catalogResult?.Steps?.Count ?? 0;
            var imageCount = catalogResult?.Images?.Count ?? 0;
            MessageBox.Show($"Plugin catalog refreshed ({stepCount} steps, {imageCount} images).", "Plugin catalog", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
