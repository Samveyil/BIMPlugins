using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.Classes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Sheets.WPF
{
    public partial class CopyStampViewModel : ObservableObject
    {
        [ObservableProperty] private string _filterForBaseSheet;
        [ObservableProperty] private string _filterForCopySheet;
        [ObservableProperty] private ICollectionView _sheetSectionsForBaseSheet;
        [ObservableProperty] private ICollectionView _sheetSectionsForCopySheet;

        partial void OnFilterForBaseSheetChanged(string value) => SheetSectionsForBaseSheet.Refresh();
        partial void OnFilterForCopySheetChanged(string value) => SheetSectionsForCopySheet.Refresh();

        public CopyStampViewModel(List<ViewSheet> sheets)
        {
            var bo = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(RevitAPI.Document);

            FolderItemInfo folderItemInfo;
            ElementId paramId;

            try
            {
                folderItemInfo = bo.GetFolderItems(sheets[0].Id)[0];

                paramId = folderItemInfo.ElementId;
            }
            catch { folderItemInfo = null; paramId = ElementId.InvalidElementId; }

            var sheetListItems = new List<SheetItem>();
            var sections = new HashSet<string>();

            foreach (var sheet in sheets)
            {
                var sheetItem = new SheetItem(sheet);

                if (folderItemInfo != null)
                {
                    var sheetParameter = sheet.ToParameter(paramId);
                    sheetItem.GroupItemName = sheetParameter.AsString().IsNullOrEmpty()
                        ? ""
                        : sheetParameter.AsString();
                }

                sections.Add(sheetItem.GroupItemName);
                sheetListItems.Add(sheetItem);
            }

            var sheetGroups = new ObservableCollection<GroupItem>();
            foreach (var section in sections)
            {
                List<SheetItem> subList = [];
                foreach (var sheetListItem in sheetListItems.OrderBy(s => s.Number, new NaturalComparer()))
                {
                    if (sheetListItem.GroupItemName == section) subList.Add(sheetListItem);
                }

                if (subList.Count > 0)
                {
                    var groupItem = new GroupItem
                    {
                        Name = section.IsNullOrEmpty()
                            ? "???"
                            : section,
                        Sheets = CollectionViewSource.GetDefaultView(subList)
                    };

                    subList.ForEach(v => v.Parent = groupItem);

                    sheetGroups.Add(groupItem);
                }
            }

            if (sheetGroups.Count == 1 && sheetGroups.First().Name == "???")
                sheetGroups.First().Name = "все";

            sheetGroups = new(sheetGroups.OrderBy(s => s.Name, new NaturalComparer()).ToList());

            SheetSectionsForBaseSheet = CollectionViewSource.GetDefaultView(sheetGroups);
            SheetSectionsForBaseSheet.Filter = item =>
            {
                var groupSheet = (GroupItem)item;

                groupSheet.Sheets.Filter = item =>
                {
                    if (FilterForBaseSheet.IsNullOrEmpty())
                        return true;

                    var sheetInstance = (SheetItem)item;
                    return sheetInstance.Title.Contains(FilterForBaseSheet, StringComparison.OrdinalIgnoreCase);
                };

                if (FilterForBaseSheet.IsNullOrEmpty())
                    return true;

                return groupSheet.Sheets.Cast<SheetItem>().Count() != 0;
            };

            var copySheetGroups = sheetGroups.Select(g => g.Clone()).ToList();
            SheetSectionsForCopySheet = CollectionViewSource.GetDefaultView(copySheetGroups);
            SheetSectionsForCopySheet.Filter = item =>
            {
                var groupSheet = (GroupItem)item;

                groupSheet.Sheets.Filter = item =>
                {
                    if (FilterForCopySheet.IsNullOrEmpty())
                        return true;

                    var sheetInstance = (SheetItem)item;
                    return sheetInstance.Title.Contains(FilterForCopySheet, StringComparison.OrdinalIgnoreCase);
                };

                if (FilterForCopySheet.IsNullOrEmpty())
                    return true;

                return groupSheet.Sheets.Cast<SheetItem>().Count() != 0;
            };
        }

        [RelayCommand]
        private void Run()
        {
            var baseSheet = SheetSectionsForBaseSheet.Cast<GroupItem>()
                .SelectMany(s => s.Sheets.Cast<SheetItem>())
                .FirstOrDefault(s => s.IsSelected == true);

            if (baseSheet == null)
            {
                MessageBox.Show("Не выбран лист с которого копируются параметры", "SE Plugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sheetsToCopy = SheetSectionsForCopySheet.Cast<GroupItem>()
                .SelectMany(s => s.Sheets.Cast<SheetItem>())
                .Where(s => s.IsSelected == true)
                .Select(s => s.Element)
                .ToList();

            if (sheetsToCopy.Count == 0)
            {
                MessageBox.Show("Не выбраны листы для копирования параметров", "SE Plugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RaiseCloseRequest();

            var titleBlock = new FilteredElementCollector(RevitAPI.Document, baseSheet.Element.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .FirstOrDefault();

            using (Transaction t = new Transaction(RevitAPI.Document, "Копировать штамп"))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Копирование штампа...", sheetsToCopy, (sheetToCopy) =>
                    {
                        foreach (var parameter in baseSheet.Element.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly).ToList())
                        {
                            string paramName = parameter.Definition.Name;
                            if (paramName.ToLower().Contains("должность") || paramName.ToLower().Contains("фамилия") || paramName.ToLower().Contains("подпись"))
                            {
                                sheetToCopy.LookupParameter(paramName).SetValue(parameter.GetValue()?? string.Empty);
                            }
                        }

                        var titleBlockToCopy = new FilteredElementCollector(RevitAPI.Document, sheetToCopy.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsNotElementType()
                            .FirstOrDefault();

                        foreach (var parameter in titleBlock.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly).ToList())
                        {
                            string paramName = parameter.Definition.Name;
                            if (paramName.ToLower().Contains("должность") || paramName.ToLower().Contains("фамилия") || paramName.ToLower().Contains("подпись") || paramName.Contains("_Дата"))
                            {
                                titleBlockToCopy.LookupParameter(paramName).SetValue(parameter.GetValue()?? string.Empty);
                            }
                        }
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                t.Commit();
            }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}