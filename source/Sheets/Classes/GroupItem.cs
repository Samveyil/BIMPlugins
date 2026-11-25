using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Sheets.Classes
{
    /// <summary>
    /// A folder of sheets as the project browser shows it. A folder of the last grouping level holds sheets,
    /// a folder of a level above holds the folders of the next level.
    /// </summary>
    public partial class GroupItem : ObservableObject
    {
        [ObservableProperty] private bool? _isSelected = false;
        [ObservableProperty] private bool _isExpanded;

        // While the folder gives its state to its items, they do not give theirs back one by one.
        private bool _isSelectingItems;

        public string Name { get; set; }

        /// <summary>
        /// The names of the folder and of the folders above it: "АР / Комплект 1".
        /// </summary>
        public string Path => Parent == null ? Name : Parent.Path + " / " + Name;

        public GroupItem Parent { get; set; }

        /// <summary>
        /// The folders of the next level, or the sheets for a folder of the last level.
        /// </summary>
        public ICollectionView Items { get; private set; }

        /// <summary>
        /// The sheets shown in the folder and in all the folders within it.
        /// </summary>
        public IEnumerable<SheetItem> Sheets => Items
            .Cast<object>()
            .SelectMany(item => item is GroupItem group ? group.Sheets : new[] { (SheetItem)item });

        public void SetItems(IEnumerable<GroupItem> groups)
        {
            var list = groups.ToList();
            list.ForEach(g => g.Parent = this);
            Items = CollectionViewSource.GetDefaultView(list);
        }

        public void SetItems(IEnumerable<SheetItem> sheets)
        {
            var list = sheets.ToList();
            list.ForEach(s => s.Parent = this);
            Items = CollectionViewSource.GetDefaultView(list);
        }

        partial void OnIsSelectedChanged(bool? value)
        {
            if (value != null)
            {
                _isSelectingItems = true;
                foreach (var item in Items)
                {
                    if (item is GroupItem group)
                        group.IsSelected = value;
                    else
                        ((SheetItem)item).IsSelected = value;
                }
                _isSelectingItems = false;
            }

            Parent?.UpdateIsSelected();
        }

        /// <summary>
        /// Takes the state of the items: selected if all of them are, not selected if none is, and undefined otherwise.
        /// </summary>
        public void UpdateIsSelected()
        {
            if (_isSelectingItems)
                return;

            var states = Items
                .Cast<object>()
                .Select(item => item is GroupItem group ? group.IsSelected : ((SheetItem)item).IsSelected)
                .ToList();

            IsSelected = states.All(s => s == true) ? true
                : states.All(s => s == false) ? false
                : null;
        }

        /// <summary>
        /// The same folders with new sheet items, for a second tree of the same sheets.
        /// </summary>
        public GroupItem Clone()
        {
            var copy = new GroupItem { Name = Name };

            var items = Items.SourceCollection.Cast<object>().ToList();
            if (items.FirstOrDefault() is GroupItem)
                copy.SetItems(items.Cast<GroupItem>().Select(g => g.Clone()));
            else
                copy.SetItems(items.Cast<SheetItem>().Select(s => new SheetItem(s)));

            return copy;
        }
    }
}
