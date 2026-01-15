using System.Collections.ObjectModel;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Represents a node in the plugins tree view.
    /// </summary>
    public sealed class PluginTreeNodeItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginTreeNodeItem"/> class.
        /// </summary>
        /// <param name="title">The display title for the node.</param>
        /// <param name="icon">Optional icon glyph for the node.</param>
        public PluginTreeNodeItem(string title, string? icon = null)
        {
            Title = title;
            Icon = icon ?? string.Empty;
        }

        /// <summary>Gets the node title.</summary>
        public string Title { get; }

        /// <summary>Gets the icon glyph for the node.</summary>
        public string Icon { get; }

        /// <summary>Gets the child nodes.</summary>
        public ObservableCollection<PluginTreeNodeItem> Children { get; } = new ObservableCollection<PluginTreeNodeItem>();

        /// <summary>Gets or sets whether this node is expanded in the UI.</summary>
        public bool IsExpanded { get; set; }
    }
}
