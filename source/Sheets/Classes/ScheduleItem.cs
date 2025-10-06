using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.WPF;
using System.Linq;

namespace BIMPlugins.Sheets.Classes
{
    public partial class ScheduleItem(SpecificationScheduleViewModel viewModel) : ObservableObject
    {
        private readonly SpecificationScheduleViewModel _viewModel = viewModel;

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

                if (_viewModel.UseTitle)
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

                _viewModel.SpecificationItems.Add(specItem);

                Parent.IsSelected = Parent.ScheduleItems.All(s => s.IsSelected == true)
                    ? true
                    : null;
            }
            else
            {
                var itemToRemove = _viewModel.SpecificationItems
                    .FirstOrDefault(item => "Спецификация: " + item.RevitName == this.Name);

                _viewModel.SpecificationItems.Remove(itemToRemove);

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