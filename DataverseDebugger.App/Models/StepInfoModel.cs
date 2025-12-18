using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataverseDebugger.App.Models
{
    /// <summary>
    /// Model representing a plugin step configuration for execution.
    /// </summary>
    /// <remarks>
    /// Contains all the information needed to invoke a plugin step, including
    /// assembly, type, message, entity, stage, mode, and configurations.
    /// </remarks>
    public class StepInfoModel : INotifyPropertyChanged
    {
        private Guid _stepId = Guid.Empty;
        private string _assembly = string.Empty;
        private string _typeName = string.Empty;
        private string _messageName = string.Empty;
        private string _primaryEntity = string.Empty;
        private int _stage = 40;
        private int _mode = 0;
        private int _rank;
        private string _filteringAttributes = string.Empty;
        private string? _unsecureConfiguration;
        private string? _secureConfiguration;

        /// <summary>Gets or sets the step registration ID.</summary>
        public Guid StepId
        {
            get => _stepId;
            set { if (_stepId != value) { _stepId = value; OnPropertyChanged(); } }
        }

        public string Assembly
        {
            get => _assembly;
            set { if (_assembly != value) { _assembly = value; OnPropertyChanged(); } }
        }

        public string TypeName
        {
            get => _typeName;
            set { if (_typeName != value) { _typeName = value; OnPropertyChanged(); } }
        }

        public string MessageName
        {
            get => _messageName;
            set { if (_messageName != value) { _messageName = value; OnPropertyChanged(); } }
        }

        public string PrimaryEntity
        {
            get => _primaryEntity;
            set { if (_primaryEntity != value) { _primaryEntity = value; OnPropertyChanged(); } }
        }

        public int Stage
        {
            get => _stage;
            set { if (_stage != value) { _stage = value; OnPropertyChanged(); } }
        }

        public int Mode
        {
            get => _mode;
            set { if (_mode != value) { _mode = value; OnPropertyChanged(); } }
        }

        public int Rank
        {
            get => _rank;
            set { if (_rank != value) { _rank = value; OnPropertyChanged(); } }
        }

        public string FilteringAttributes
        {
            get => _filteringAttributes;
            set { if (_filteringAttributes != value) { _filteringAttributes = value; OnPropertyChanged(); } }
        }

        public string? UnsecureConfiguration
        {
            get => _unsecureConfiguration;
            set { if (_unsecureConfiguration != value) { _unsecureConfiguration = value; OnPropertyChanged(); } }
        }

        public string? SecureConfiguration
        {
            get => _secureConfiguration;
            set { if (_secureConfiguration != value) { _secureConfiguration = value; OnPropertyChanged(); } }
        }

        public string Summary => $"{MessageName} | {PrimaryEntity} | Stage {Stage}, Mode {Mode}";

        public string Display
        {
            get
            {
                var asm = string.IsNullOrWhiteSpace(Assembly) ? string.Empty : $" ({Assembly})";
                var meta = $"{MessageName} / {PrimaryEntity}".Trim().Trim('/', ' ');
                var stageMode = $"Stage {Stage}, Mode {Mode}";
                return string.IsNullOrWhiteSpace(meta)
                    ? $"{TypeName}{asm} [{stageMode}]"
                    : $"{TypeName}{asm} [{meta}; {stageMode}]";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
