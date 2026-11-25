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
                    RevitName = this.Name.Replace("Спецификация: ", "")
                };

                if (_vm.UseTitle)
                {
                    var headerData = Element.GetTableData().GetSectionData(SectionType.Header);
                    var title = headerData.GetCellText(0, 0);
                    specItem.Title = title.IsNullOrEmpty()
                        ? specItem.RevitName.Trim()
                        : title.Replace("\r\n", " ").Trim();
                }
                else
                {
                    specItem.Title = specItem.RevitName.Trim();
                }

                _vm.SpecificationItems.Add(specItem);

                Parent.IsSelected = Parent.ScheduleItems.All(s => s.IsSelected == true)
                    ? true
                    : null;
            }
            else
            {
                var itemToRemove = _vm.SpecificationItems
                    .FirstOrDefault(item => "Спецификация: " + item.RevitName == this.Name);

                _vm.SpecificationItems.Remove(itemToRemove);

                Parent.IsSelected = Parent.ScheduleItems.Any(s => s.IsSelected == true)
                    ? null
                    : false;
            }
        }

        public string Name { get; set; }
        public ViewSchedule Element { get; set; }
        public SheetScheduleItem Parent { get; set; }
    }
}