using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DataverseDebugger.App.Models;
using DataverseDebugger.App.Services;

namespace DataverseDebugger.App.Views
{
    /// <summary>
    /// View for displaying plugin registrations from the active environment.
    /// </summary>
    public partial class PluginsView : UserControl
    {
        private const int WarningListLimit = 5;
        private const string AssemblyIcon = "\U0001F4E6";
        private const string PluginIcon = "\U0001F50C";
        private const string StepIcon = "\u2699";
        private const string ImageIcon = "\U0001F5BC";

        public ObservableCollection<PluginTreeNodeItem> PluginTreeItems { get; } = new ObservableCollection<PluginTreeNodeItem>();

        public PluginsView()
        {
            InitializeComponent();
            DataContext = this;
            ShowNoEnvironmentState();
        }

        public void ApplyCatalog(PluginCatalog catalog, IReadOnlyList<string> selectedPluginEntries)
        {
            if (catalog == null)
            {
                ShowCatalogUnavailable("Catalog was not provided.");
                return;
            }

            PluginTreeItems.Clear();

            var selectedNames = NormalizeSelectedAssemblyNames(selectedPluginEntries);
            if (selectedNames.Count == 0)
            {
                ShowInfo("No plugins selected in Environment Settings.", null, null);
                return;
            }

            var warningMessage = BuildMismatchWarning(selectedNames, catalog.Assemblies);
            var assemblies = FilterAssemblies(catalog.Assemblies, selectedNames);
            if (assemblies.Count == 0)
            {
                ShowInfo("No plugin registrations found for the selected plugins.", null, warningMessage);
                return;
            }

            var tree = BuildTree(catalog, assemblies);
            foreach (var node in tree)
            {
                PluginTreeItems.Add(node);
            }

            ShowReady(warningMessage);
        }

        public void ShowCatalogUnavailable(string? errorMessage)
        {
            PluginTreeItems.Clear();
            ShowError("Plugin catalog is unavailable.", errorMessage);
        }

        public void Clear()
        {
            PluginTreeItems.Clear();
            ShowNoEnvironmentState();
        }

        private static HashSet<string> NormalizeSelectedAssemblyNames(IReadOnlyList<string> selectedPluginEntries)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (selectedPluginEntries == null)
            {
                return result;
            }

            foreach (var entry in selectedPluginEntries)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                var trimmed = entry.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                string? candidate = null;
                try
                {
                    candidate = Path.GetFileNameWithoutExtension(trimmed);
                }
                catch (Exception)
                {
                    candidate = null;
                }

                var normalized = string.IsNullOrWhiteSpace(candidate) ? trimmed : candidate;
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        private static string? BuildMismatchWarning(HashSet<string> selectedNames, IEnumerable<PluginAssemblyItem> catalogAssemblies)
        {
            if (selectedNames.Count == 0)
            {
                return null;
            }

            var catalogNames = new HashSet<string>(
                catalogAssemblies
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .Select(a => a.Name),
                StringComparer.OrdinalIgnoreCase);

            var unmatched = selectedNames
                .Where(name => !catalogNames.Contains(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unmatched.Count == 0)
            {
                return null;
            }

            var preview = unmatched.Take(WarningListLimit).ToList();
            var remaining = unmatched.Count - preview.Count;
            var list = string.Join(", ", preview);
            if (remaining > 0)
            {
                list = $"{list} (+{remaining} more)";
            }

            return $"Warning: Some selected plugin names did not match any plugin assemblies in Dataverse: {list}";
        }

        private static List<PluginAssemblyItem> FilterAssemblies(IEnumerable<PluginAssemblyItem> assemblies, HashSet<string> selectedNames)
        {
            return assemblies
                .Where(a => !string.IsNullOrWhiteSpace(a.Name) && selectedNames.Contains(a.Name))
                .GroupBy(a => a.Id)
                .Select(g => g.First())
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Id)
                .ToList();
        }

        private static List<PluginTreeNodeItem> BuildTree(PluginCatalog catalog, List<PluginAssemblyItem> assemblies)
        {
            var assemblyNodes = new List<PluginTreeNodeItem>();
            var assemblyIdByName = assemblies
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
            var assemblyIds = new HashSet<Guid>(assemblies.Select(a => a.Id));

            var types = FilterTypes(catalog.Types, assemblyIds, assemblyIdByName, out var typeAssemblyIds);

            var steps = FilterSteps(catalog.Steps, assemblyIds, typeAssemblyIds);
            var stepsByType = steps
                .GroupBy(s => s.PluginTypeId)
                .ToDictionary(g => g.Key, g => g.ToList());
            var imagesByStep = FilterImages(catalog.Images, steps);

            foreach (var assembly in assemblies)
            {
                var assemblyNode = new PluginTreeNodeItem(BuildAssemblyLabel(assembly), AssemblyIcon)
                {
                    IsExpanded = true
                };

                var assemblyTypes = types
                    .Where(t => typeAssemblyIds.TryGetValue(t.Id, out var typeAssemblyId) && typeAssemblyId == assembly.Id)
                    .OrderBy(t => t.TypeName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(t => t.Id)
                    .ToList();

                foreach (var type in assemblyTypes)
                {
                    var typeNode = new PluginTreeNodeItem(BuildTypeLabel(type), PluginIcon);
                    if (stepsByType.TryGetValue(type.Id, out var typeSteps))
                    {
                        foreach (var step in typeSteps
                            .OrderBy(s => s.Stage)
                            .ThenBy(s => s.Rank)
                            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(s => s.Id))
                        {
                            var stepNode = new PluginTreeNodeItem(BuildStepLabel(step, type), StepIcon)
                            {
                                IsExpanded = false
                            };
                            if (imagesByStep.TryGetValue(step.Id, out var stepImages))
                            {
                                foreach (var image in stepImages
                                    .OrderBy(i => i.ImageType, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(i => i.EntityAlias, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(i => i.Attributes, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(i => i.Id))
                                {
                                    stepNode.Children.Add(new PluginTreeNodeItem(BuildImageLabel(image, step), ImageIcon)
                                    {
                                        IsExpanded = false
                                    });
                                }
                            }
                            typeNode.Children.Add(stepNode);
                        }
                    }

                    assemblyNode.Children.Add(typeNode);
                }

                assemblyNodes.Add(assemblyNode);
            }

            return assemblyNodes;
        }

        private static List<PluginTypeItem> FilterTypes(
            IEnumerable<PluginTypeItem> types,
            HashSet<Guid> selectedAssemblyIds,
            Dictionary<string, Guid> assemblyIdByName,
            out Dictionary<Guid, Guid> typeAssemblyIds)
        {
            var filtered = new Dictionary<Guid, PluginTypeItem>();
            typeAssemblyIds = new Dictionary<Guid, Guid>();
            foreach (var type in types)
            {
                var assemblyId = type.AssemblyId;
                if (assemblyId == Guid.Empty && !string.IsNullOrWhiteSpace(type.AssemblyName))
                {
                    assemblyIdByName.TryGetValue(type.AssemblyName, out assemblyId);
                }

                if (assemblyId == Guid.Empty || !selectedAssemblyIds.Contains(assemblyId))
                {
                    continue;
                }

                if (!filtered.ContainsKey(type.Id))
                {
                    filtered[type.Id] = type;
                    typeAssemblyIds[type.Id] = assemblyId;
                }
            }

            return filtered.Values
                .OrderBy(t => t.TypeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Id)
                .ToList();
        }

        private static List<PluginStepItem> FilterSteps(
            IEnumerable<PluginStepItem> steps,
            HashSet<Guid> selectedAssemblyIds,
            Dictionary<Guid, Guid> typeAssemblyIds)
        {
            var filtered = new Dictionary<Guid, PluginStepItem>();
            foreach (var step in steps)
            {
                if (!typeAssemblyIds.TryGetValue(step.PluginTypeId, out var assemblyId))
                {
                    continue;
                }

                if (!selectedAssemblyIds.Contains(assemblyId))
                {
                    continue;
                }

                if (!filtered.ContainsKey(step.Id))
                {
                    filtered[step.Id] = step;
                }
            }

            return filtered.Values
                .OrderBy(s => s.Stage)
                .ThenBy(s => s.Rank)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Id)
                .ToList();
        }

        private static Dictionary<Guid, List<PluginImageItem>> FilterImages(IEnumerable<PluginImageItem> images, IEnumerable<PluginStepItem> steps)
        {
            var stepIds = new HashSet<Guid>(steps.Select(s => s.Id));
            return images
                .Where(i => stepIds.Contains(i.StepId))
                .GroupBy(i => i.StepId)
                .ToDictionary(g => g.Key, g => g.GroupBy(i => i.Id).Select(x => x.First()).ToList());
        }

        private static string BuildAssemblyLabel(PluginAssemblyItem assembly)
        {
            if (!string.IsNullOrWhiteSpace(assembly.Version))
            {
                return $"(Assembly) {assembly.Name} (v{assembly.Version})";
            }

            return $"(Assembly) {assembly.Name}";
        }

        private static string BuildTypeLabel(PluginTypeItem type)
        {
            return $"(Plugin) {type.TypeName}";
        }

        private static string BuildStepLabel(PluginStepItem step, PluginTypeItem type)
        {
            var messageLabel = BuildMessageLabel(step);
            return $"(Step) {type.TypeName}: {messageLabel} (OpOrder: {step.Stage}, Rank: {step.Rank})";
        }

        private static string BuildMessageLabel(PluginStepItem step)
        {
            var message = step.Message ?? string.Empty;
            var primary = step.PrimaryEntity ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = step.Name ?? string.Empty;
            }
            if (!string.IsNullOrWhiteSpace(primary) && !string.Equals(primary, "none", StringComparison.OrdinalIgnoreCase))
            {
                return $"{message} of {primary}";
            }

            return message;
        }

        private static string BuildImageLabel(PluginImageItem image, PluginStepItem step)
        {
            var label = $"(Image) {image.ImageType}";
            if (!string.IsNullOrWhiteSpace(image.Attributes))
            {
                label += $" ({image.Attributes})";
            }

            var descriptor = BuildImageEntityDescriptor(step);
            if (!string.IsNullOrWhiteSpace(descriptor))
            {
                label += $" - {descriptor}";
            }

            return label;
        }

        private static string BuildImageEntityDescriptor(PluginStepItem step)
        {
            var message = step.Message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = step.Name ?? string.Empty;
            }

            if (message.Equals("Create", StringComparison.OrdinalIgnoreCase))
            {
                return "Created Entity";
            }

            if (message.Equals("Update", StringComparison.OrdinalIgnoreCase))
            {
                return "Updated Entity";
            }

            if (message.Equals("Delete", StringComparison.OrdinalIgnoreCase))
            {
                return "Deleted Entity";
            }

            var primary = step.PrimaryEntity ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(primary) && !string.Equals(primary, "none", StringComparison.OrdinalIgnoreCase))
            {
                return primary;
            }

            return string.Empty;
        }

        private void ShowNoEnvironmentState()
        {
            ShowInfo("Select an environment to view plugin registrations.", null, null);
        }

        private void ShowReady(string? warning)
        {
            SetStatus(null, null, null, warning);
        }

        private void ShowInfo(string message, string? details, string? warning)
        {
            SetStatus(StatusTone.Info, message, details, warning);
        }

        private void ShowError(string message, string? details)
        {
            SetStatus(StatusTone.Error, message, details, null);
        }

        private void SetStatus(StatusTone? tone, string? message, string? details, string? warning)
        {
            var hasMessage = !string.IsNullOrWhiteSpace(message);
            var hasWarning = !string.IsNullOrWhiteSpace(warning);

            StatusPrimaryRow.Visibility = hasMessage ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = message ?? string.Empty;
            StatusDetailText.Text = details ?? string.Empty;
            StatusDetailText.Visibility = string.IsNullOrWhiteSpace(details) ? Visibility.Collapsed : Visibility.Visible;
            StatusWarningText.Text = warning ?? string.Empty;
            StatusWarningText.Visibility = hasWarning ? Visibility.Visible : Visibility.Collapsed;

            if (hasMessage)
            {
                if (tone == StatusTone.Error)
                {
                    var errorBrush = (Brush)FindResource("ThemeErrorBrush");
                    StatusIcon.Text = "!";
                    StatusIcon.Foreground = errorBrush;
                    StatusText.Foreground = errorBrush;
                }
                else
                {
                    var infoBrush = (Brush)FindResource("ThemeTextSecondaryBrush");
                    StatusIcon.Text = "i";
                    StatusIcon.Foreground = infoBrush;
                    StatusText.Foreground = infoBrush;
                }
            }

            StatusPanel.Visibility = (hasMessage || hasWarning) ? Visibility.Visible : Visibility.Collapsed;
        }

        private enum StatusTone
        {
            Info,
            Error
        }
    }
}
