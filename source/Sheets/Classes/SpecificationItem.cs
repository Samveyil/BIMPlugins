using CommunityToolkit.Mvvm.ComponentModel;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SpecificationItem : ObservableObject
    {
        [ObservableProperty] private string _title;
        [ObservableProperty] private string _sheetNumber;

        public string RevitName { get; set; }
        public ScheduleItem ScheduleInstance { get; set; }

        public string SheetNumberFromExcel { get; set; }
    }
}