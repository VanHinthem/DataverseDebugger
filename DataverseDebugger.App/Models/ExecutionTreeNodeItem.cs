using System.Collections.ObjectModel;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Represents a node in the plugin execution tree view.
    /// </summary>
    /// <remarks>
    /// Used to display hierarchical plugin step information in the requests view.
    /// </remarks>
    public sealed class ExecutionTreeNodeItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExecutionTreeNodeItem"/> class.
        /// </summary>
        /// <param name="title">The display title for the node.</param>
        /// <param name="step">Optional step info associated with this node.</param>
        public ExecutionTreeNodeItem(string title, StepInfoModel? step = null)
        {
            Title = title;
            Step = step;
        }

        /// <summary>Gets the node title.</summary>
        public string Title { get; }

        /// <summary>Gets or sets the tooltip text.</summary>
        public string? ToolTip { get; set; }

        /// <summary>Gets or sets whether this node can be selected.</summary>
        public bool IsSelectable { get; set; } = true;

        /// <summary>Gets the associated step info, if any.</summary>
        public StepInfoModel? Step { get; }

        /// <summary>Gets the child nodes.</summary>
        public ObservableCollection<ExecutionTreeNodeItem> Children { get; } = new ObservableCollection<ExecutionTreeNodeItem>();

        /// <summary>Gets or sets whether this node is expanded in the UI.</summary>
        public bool IsExpanded { get; set; } = true;
    }
}
