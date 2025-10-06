using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Sheets.Classes
{
    public partial class GroupItem : ObservableObject
    {
        [ObservableProperty] private bool? _isSelected = false;
        [ObservableProperty] private ICollectionView _sheets;

        partial void OnIsSelectedChanged(bool? value)
        {
            if (value != null)
            {
                Sheets.Cast<SheetItem>().ToList().ForEach(v => v.IsSelected = (bool)value);
            }
        }

        public string Name { get; set; }

        public GroupItem Clone()
        {
            var copy = new GroupItem();
            copy.Name = Name;
            copy.Sheets = CollectionViewSource.GetDefaultView(Sheets.Cast<SheetItem>().Select(s => new SheetItem(s) { Parent = copy }).ToList());

            return copy;
        }
    }
}