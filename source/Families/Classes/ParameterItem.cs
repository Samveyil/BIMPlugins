using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BIMPlugins.Families.Classes
{
    public partial class ParameterItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected = true;
        [ObservableProperty] private int _modeIndex = 0;
        
        [ObservableProperty] private int _fixedValue;

        [ObservableProperty] private int _minValue = 10;
        [ObservableProperty] private int _maxValue = 1000;
        [ObservableProperty] private int _step = 10;

        [ObservableProperty] private string _values;

        public FamilySizeTable FamilySizeTable { get; set; }
        public int ColumnNumber { get; set; } = 1;

        public ParameterItem(FamilyParameter parameter)
        {
            var def = parameter.Definition;

            Parameter = parameter;
            Name = def.Name;
            Group = LabelUtils.GetLabelFor(def.ParameterGroup);

#if R2022_OR_GREATER
            IsYesNo = def.GetDataType() == SpecTypeId.Boolean.YesNo;
            if (def.GetDataType() == SpecTypeId.Angle)
            {
                MinValue = 5;
                MaxValue = 87;
                FixedValue = 30;
            }
#else
            IsYesNo = def.ParameterType == ParameterType.YesNo;
            if (def.ParameterType == ParameterType.Angle)
            {
                MinValue = 5;
                MaxValue = 87;
                FixedValue = 30;
            }
#endif
            FixedValue = IsYesNo ? 1 : 100;
        }

        public FamilyParameter Parameter { get; set; }
        public string Name { get; }
        public string Group { get; } 
        public bool IsYesNo { get; }
    }
}