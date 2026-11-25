using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using Visibility = System.Windows.Visibility;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetNumberingItem : SheetItem
    {
        [ObservableProperty][NotifyPropertyChangedFor(nameof(NewNumberMark))] private string _newNumber;
        [ObservableProperty] private string _numberToParameter;
        [ObservableProperty] private Visibility _warningImageVisibility = Visibility.Collapsed;

        public string Name { get; set; }

        public string NumberMark => InvisibleChars.Mark(Number);
        public string NewNumberMark => InvisibleChars.Mark(NewNumber);

        public SheetNumberingItem(ViewSheet viewSheet) : base(viewSheet)
        {
            Name = viewSheet.Name;
            NewNumber = Number;
        }
    }
}
