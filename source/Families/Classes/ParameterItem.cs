using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BIMPlugins.Families.Classes
{
    public partial class ParameterItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected = true;
        [ObservableProperty] private int _modeIndex = 0;
        
        [ObservableProperty] private bool isFixed = false;
        [ObservableProperty] private int _fixedValue = 100;

        [ObservableProperty] private int _minValue = 10;
        [ObservableProperty] private int _maxValue = 1000;
        [ObservableProperty] private int _step = 10;

        partial void OnModeIndexChanged(int value) => IsFixed = value == 1;

        public ParameterItem(FamilyParameter parameter)
        {
            var def = parameter.Definition;

            Parameter = parameter;
            Name = def.Name;
            Group = LabelUtils.GetLabelFor(def.ParameterGroup);
            IsYesNo = def.ParameterType == ParameterType.YesNo;
            FixedValue = IsYesNo ? 1 : 100;

            if (def.ParameterType == ParameterType.Angle)
            {
                MinValue = 5;
                MaxValue = 87;
                FixedValue = 30;
            }
        }

        public FamilyParameter Parameter { get; set; }
        public string Name { get; }
        public string Group { get; } 
        public bool IsYesNo { get; }
    }
}
