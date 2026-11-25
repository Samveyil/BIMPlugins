using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Sheets.Classes
{
    /// <summary>
    /// Sheets grouped and sorted as the project browser shows them: folders within folders for every grouping level
    /// of its organization of sheets, and sheets by its sorting parameter and order.
    /// </summary>
    public static class SheetGroups
    {
        private const string NoGroupName = "???";
        private const string AllSheetsName = "все";

        /// <summary>
        /// The folders of the first grouping level, with the folders of the next levels within them and the sheets in the last ones.
        /// With no grouping there is one folder, "все".
        /// </summary>
        public static List<GroupItem> Create<T>(Document doc, IList<T> sheetItems) where T : SheetItem
        {
            var organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);
            var sortingParameterId = organization.SortingParameterId;
            var descending = organization.SortingOrder == SortingOrder.Descending;

            // The folder names already have what the organization makes of the values, such as their first characters only.
            var folders = sheetItems.ToDictionary(
                s => (SheetItem)s,
                s => organization.GetFolderItems(s.Element.Id).Select(f => f.Name.IsNullOrEmpty() ? NoGroupName : f.Name).ToList());

            IEnumerable<SheetItem> Sort(IEnumerable<SheetItem> sheets) => descending
                ? sheets.OrderByDescending(s => GetSortValue(s.Element, sortingParameterId), NaturalComparer.CurrentCultureIgnoreCase)
                : sheets.OrderBy(s => GetSortValue(s.Element, sortingParameterId), NaturalComparer.CurrentCultureIgnoreCase);

            List<GroupItem> CreateLevel(IEnumerable<SheetItem> sheets, int level) => sheets
                .GroupBy(s => level < folders[s].Count ? folders[s][level] : NoGroupName)
                .OrderBy(g => g.Key, NaturalComparer.CurrentCultureIgnoreCase)
                .Select(g =>
                {
                    var group = new GroupItem { Name = g.Key };

                    // The sheets are in the folder of the last level; a level above holds folders.
                    if (g.Any(s => level + 1 < folders[s].Count))
                        group.SetItems(CreateLevel(g, level + 1));
                    else
                        group.SetItems(Sort(g));

                    return group;
                })
                .ToList();

            var groups = CreateLevel(sheetItems.Cast<SheetItem>(), 0);

            if (groups.Count == 1 && groups[0].Name == NoGroupName)
                groups[0].Name = AllSheetsName;

            return groups;
        }

        /// <summary>
        /// The folders with the sheets filtered by their title; the folders left empty by the filter are hidden.
        /// </summary>
        public static ICollectionView CreateView(List<GroupItem> groups, Func<string> filter)
        {
            bool IsShown(SheetItem sheet) => filter().IsNullOrEmpty() || sheet.Title.Contains(filter(), StringComparison.OrdinalIgnoreCase);

            // A folder shows its items as they are after the filter, so it refreshes them before telling whether it is empty.
            bool HasItems(GroupItem group)
            {
                group.Items.Refresh();
                return !group.Items.IsEmpty;
            }

            void SetFilter(GroupItem group)
            {
                group.Items.Filter = item => item is GroupItem subgroup ? HasItems(subgroup) : IsShown((SheetItem)item);

                foreach (var subgroup in group.Items.SourceCollection.OfType<GroupItem>())
                    SetFilter(subgroup);
            }

            groups.ForEach(SetFilter);

            var view = CollectionViewSource.GetDefaultView(groups);
            view.Filter = item => HasItems((GroupItem)item);

            return view;
        }

        /// <summary>
        /// Collapses the folders and the sheets within them, the ones hidden by a filter too.
        /// </summary>
        public static void CollapseAll(IEnumerable groups)
        {
            foreach (var group in groups.Cast<GroupItem>())
            {
                group.IsExpanded = false;

                foreach (var item in group.Items.SourceCollection)
                {
                    if (item is SheetItem sheet)
                        sheet.IsExpanded = false;
                }

                CollapseAll(group.Items.SourceCollection.OfType<GroupItem>());
            }
        }

        // The value the browser sorts by, as text; the sheet number if the organization names no parameter.
        private static string GetSortValue(ViewSheet sheet, ElementId parameterId)
        {
            if (parameterId == null || parameterId == ElementId.InvalidElementId)
                return sheet.SheetNumber;

            var parameter = sheet.ToParameter(parameterId);
            if (parameter == null)
                return "";

            return (parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString()) ?? "";
        }
    }
}
