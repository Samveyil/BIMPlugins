using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetItem : ObservableObject
    {
        private bool? _isSelected = false;

        [ObservableProperty] private bool _isExpanded;

        public virtual bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                    Parent?.UpdateIsSelected();
            }
        }

        public string Title { get; set; }
        public string Number { get; set; }
        public GroupItem Parent { get; set; }
        public ViewSheet Element { get; set; }

        public SheetItem(ViewSheet viewSheet)
        {
            Element = viewSheet;
            Title = viewSheet.Title;
            Number = viewSheet.SheetNumber;
        }
    }
}
