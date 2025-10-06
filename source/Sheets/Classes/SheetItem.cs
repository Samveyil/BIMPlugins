using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Linq;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetItem : ObservableObject
    {
        private bool? _isSelected = false;

        public virtual bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    if (Parent != null)
                    {
                        if (value == null)
                            Parent.IsSelected = null;
                        else
                        {
                            bool allSelected = Parent.Sheets.Cast<SheetItem>().All(s => s.IsSelected == true);
                            bool anySelected = Parent.Sheets.Cast<SheetItem>().Any(s => s.IsSelected == true);

                            Parent.IsSelected = allSelected ? true : (anySelected ? null : false);
                        }
                            
                    }
                }
            }
        }
        
        public string Title { get; set; }
        public string Number { get; set; }
        public GroupItem Parent { get; set; }
        public string GroupItemName { get; set; }
        public ViewSheet Element { get; set; }

        public SheetItem(ViewSheet viewSheet)
        {
            Element = viewSheet;
            Title = viewSheet.Title;
            Number = viewSheet.SheetNumber;
        }
        public SheetItem(SheetItem other)
        {
            Element = other.Element;
            Number = other.Number;
            Title = other.Title;
        }
    }
}