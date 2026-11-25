using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.WPF;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace BIMPlugins.Sheets.Classes
{
    public partial class ScheduleItem(SpecificationScheduleVM vm) : ObservableObject
    {
        private readonly SpecificationScheduleVM _vm = vm;

        [ObservableProperty] private bool _isSelected;

        partial void OnIsSelectedChanged(bool value)
        {
            if (value)
            {
                var specItem = new SpecificationItem
                {
                    ScheduleInstance = this,
                    SheetNumber = Parent.NumberFromParameter,
                    RevitName = ScheduleName,
                    Title = GetTitle()
                };

                _vm.SpecificationItems.Add(specItem);

                Parent.IsSelected = Parent.ScheduleItems.All(s => s.IsSelected) ? true : null;
            }
            else
            {
                // The row of this very schedule on this sheet: one schedule may stand on several sheets under one name.
                _vm.SpecificationItems.Remove(_vm.SpecificationItems.FirstOrDefault(item => item.ScheduleInstance == this));

                Parent.IsSelected = Parent.ScheduleItems.Any(s => s.IsSelected) ? null : false;
            }
        }

        // The title of the schedule's header if asked and there is one, otherwise its name.
        private string GetTitle()
        {
            if (_vm.UseTitle)
            {
                var title = Element.GetTableData().GetSectionData(SectionType.Header).GetCellText(0, 0);
                if (!title.IsNullOrEmpty())
                    return title.Replace("\r\n", " ").Trim();
            }

            return ScheduleName.Trim();
        }

        public string Name => "Спецификация: " + ScheduleName;
        public string ScheduleName { get; set; }
        public ViewSchedule Element { get; set; }
        public SheetScheduleItem Parent { get; set; }
    }
}
