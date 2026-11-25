using CommunityToolkit.Mvvm.ComponentModel;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SpecificationItem : ObservableObject
    {
        [ObservableProperty] private string _title;
        [ObservableProperty] private string _sheetNumber;

        public string RevitName { get; set; }

        /// <summary>The schedule on a sheet the row is for; null for a row from Excel.</summary>
        public ScheduleItem ScheduleInstance { get; set; }

        public bool IsFromExcel => ScheduleInstance == null;
        public string SheetNumberFromExcel { get; set; }
    }
}
