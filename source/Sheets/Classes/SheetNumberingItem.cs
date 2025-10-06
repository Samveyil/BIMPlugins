using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetNumberingItem : SheetItem
    {
        [ObservableProperty] private string _newNumber;
        [ObservableProperty] private string _numberToParameter;
        [ObservableProperty] private System.Windows.Visibility _warningImageVisibility = System.Windows.Visibility.Collapsed;

        public string Name { get; set; }
        public List<TitleBlock> TitleBlocks { get; set; } = [];

        public SheetNumberingItem(ViewSheet viewSheet) : base(viewSheet)
        {
            Name = viewSheet.Name;
            NewNumber = Number;
        }
    }
}