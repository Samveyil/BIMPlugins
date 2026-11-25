using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using Visibility = System.Windows.Visibility;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetNumberingItem : SheetItem
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(NewNumberInvisibleCount), nameof(HasNewNumberInvisibleChars))]
        private string _newNumber;

        [ObservableProperty] private string _numberToParameter;
        [ObservableProperty] private Visibility _warningImageVisibility = Visibility.Collapsed;

        public string Name { get; set; }

        // The value the chosen parameter has now, shown when the sheet is not renumbered.
        public string ParameterValue { get; set; }

        public int NumberInvisibleCount { get; }
        public bool HasNumberInvisibleChars => NumberInvisibleCount != 0;

        public int NewNumberInvisibleCount => InvisibleChars.Count(NewNumber);
        public bool HasNewNumberInvisibleChars => NewNumberInvisibleCount != 0;

        public SheetNumberingItem(ViewSheet viewSheet) : base(viewSheet)
        {
            Name = viewSheet.Name;
            NewNumber = Number;
            NumberInvisibleCount = InvisibleChars.Count(Number);
        }
    }
}
