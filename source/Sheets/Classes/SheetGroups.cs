using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Sheets.Classes
{
    /// <summary>
    /// Sheets grouped and sorted as the project browser shows them: by every grouping level of its organization of sheets,
    /// and by its sorting parameter and order.
    /// </summary>
    public static class SheetGroups
    {
        private const string LevelSeparator = " / ";
        private const string NoGroupName = "???";
        private const string AllSheetsName = "все";

        /// <summary>
        /// Groups the sheet items as the project browser does. A group is named by its folders on all the levels,
        /// "АР / Комплект 1"; each item gets its group name and parent. With no grouping there is one group, "все".
        /// </summary>
        public static List<GroupItem> Create<T>(Document doc, IList<T> sheetItems) where T : SheetItem
        {
            var organization = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(doc);
            var sortingParameterId = organization.SortingParameterId;
            var descending = organization.SortingOrder == SortingOrder.Descending;

            // The folder names already have what the organization makes of the values, such as their first characters only.
            foreach (var sheetItem in sheetItems)
            {
                sheetItem.GroupItemName = string.Join(LevelSeparator, organization.GetFolderItems(sheetItem.Element.Id)
                    .Select(f => f.Name.IsNullOrEmpty() ? NoGroupName : f.Name));
            }

            var groups = sheetItems
                .GroupBy(s => s.GroupItemName)
                .Select(g =>
                {
                    var sheets = descending
                        ? g.OrderByDescending(s => GetSortValue(s.Element, sortingParameterId), NaturalComparer.CurrentCultureIgnoreCase).ToList()
                        : g.OrderBy(s => GetSortValue(s.Element, sortingParameterId), NaturalComparer.CurrentCultureIgnoreCase).ToList();

                    var group = new GroupItem
                    {
                        Name = g.Key.IsNullOrEmpty() ? NoGroupName : g.Key,
                        Sheets = CollectionViewSource.GetDefaultView(sheets)
                    };

                    sheets.ForEach(s => s.Parent = group);
                    return group;
                })
                .OrderBy(g => g.Name, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (groups.Count == 1 && groups[0].Name == NoGroupName)
                groups[0].Name = AllSheetsName;

            return groups;
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
