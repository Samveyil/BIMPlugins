using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetScheduleItem(ViewSheet viewSheet) : SheetItem(viewSheet)
    {
        [ObservableProperty] private string _numberFromParameter;

        public override bool? IsSelected
        {
            get => base.IsSelected;
            set
            {
                base.IsSelected = value;
                
                if (value != null)
                    ScheduleItems.ForEach(v => v.IsSelected = (bool)value);
            }
        }

        public List<ScheduleItem> ScheduleItems { get; set; } = [];
    }
}