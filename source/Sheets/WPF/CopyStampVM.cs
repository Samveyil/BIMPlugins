using Autodesk.Revit.DB;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace BIMPlugins.Sheets.WPF
{
    public partial class CopyStampVM : ObservableObject
    {
        private const string Title = "Копировать штамп";

        // Parameters of the stamp: who signed and when.
        private static readonly string[] StampParameterWords = ["должность", "фамилия", "подпись"];
        private const string TitleBlockDateMark = "_Дата";

        [ObservableProperty] private string _filterForBaseSheet;
        [ObservableProperty] private string _filterForCopySheet;
        [ObservableProperty] private ICollectionView _sheetSectionsForBaseSheet;
        [ObservableProperty] private ICollectionView _sheetSectionsForCopySheet;

        private readonly Document _doc;

        partial void OnFilterForBaseSheetChanged(string value) => SheetSectionsForBaseSheet.Refresh();
        partial void OnFilterForCopySheetChanged(string value) => SheetSectionsForCopySheet.Refresh();

        public CopyStampVM(IList<ViewSheet> sheets)
        {
            _doc = RevitAPI.Document;

            var baseGroups = SheetGroups.Create(_doc, sheets.Select(s => new SheetItem(s)).ToList());
            SheetSectionsForBaseSheet = SheetGroups.CreateView(baseGroups, () => FilterForBaseSheet);

            var copyGroups = baseGroups.Select(g => g.Clone()).ToList();
            SheetSectionsForCopySheet = SheetGroups.CreateView(copyGroups, () => FilterForCopySheet);
        }

        [RelayCommand]
        private void CollapseBaseSheets() => SheetGroups.CollapseAll(SheetSectionsForBaseSheet.SourceCollection);

        [RelayCommand]
        private void CollapseCopySheets() => SheetGroups.CollapseAll(SheetSectionsForCopySheet.SourceCollection);

        [RelayCommand]
        private void Run()
        {
            var baseSheet = SheetSectionsForBaseSheet.Cast<GroupItem>()
                .SelectMany(g => g.Sheets)
                .FirstOrDefault(s => s.IsSelected == true)?
                .Element;

            if (baseSheet == null)
            {
                MessageDialog.Show("Не выбран лист, с которого копируются параметры!", Title, icon: MessageIcon.Warning);
                return;
            }

            var sheetsToCopy = SheetSectionsForCopySheet.Cast<GroupItem>()
                .SelectMany(g => g.Sheets)
                .Where(s => s.IsSelected == true)
                .Select(s => s.Element)
                .ToList();

            if (sheetsToCopy.Count == 0)
            {
                MessageDialog.Show("Не выбраны листы для копирования параметров!", Title, icon: MessageIcon.Warning);
                return;
            }

            var titleBlock = _doc.ToElements(baseSheet.Id, BuiltInCategory.OST_TitleBlocks).FirstOrDefault();
            if (titleBlock == null)
            {
                MessageDialog.Show("На выбранном листе нет штампа!", Title, icon: MessageIcon.Warning);
                return;
            }

            RaiseCloseRequest();

            // The parameters to copy are the same for every sheet: they are picked once.
            var sheetParameters = GetStampParameters(baseSheet, withDate: false);
            var titleBlockParameters = GetStampParameters(titleBlock, withDate: true);

            var sheetsWithoutTitleBlock = new List<string>();

            using (Transaction t = new Transaction(_doc, Title))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Копирование штампа...", sheetsToCopy, sheetToCopy =>
                    {
                        CopyParameters(sheetParameters, sheetToCopy);

                        var titleBlockToCopy = _doc.ToElements(sheetToCopy.Id, BuiltInCategory.OST_TitleBlocks).FirstOrDefault();
                        if (titleBlockToCopy != null)
                            CopyParameters(titleBlockParameters, titleBlockToCopy);
                        else
                            sheetsWithoutTitleBlock.Add(sheetToCopy.SheetNumber);
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                t.Commit();
            }

            if (sheetsWithoutTitleBlock.Count != 0)
                MessageDialog.Show($"На листах без штампа скопированы только параметры листа: {string.Join(", ", sheetsWithoutTitleBlock)}",
                    Title, icon: MessageIcon.Information);
        }

        private static List<Parameter> GetStampParameters(Element element, bool withDate) => element.Parameters
            .Cast<Parameter>()
            .Where(p => !p.IsReadOnly)
            .Where(p =>
            {
                var name = p.Definition.Name;
                return StampParameterWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))
                    || withDate && name.Contains(TitleBlockDateMark);
            })
            .ToList();

        // A sheet or a title block without one of the parameters simply keeps it unset.
        private static void CopyParameters(List<Parameter> sourceParameters, Element target)
        {
            foreach (var parameter in sourceParameters)
                target.LookupParameter(parameter.Definition.Name)?.SetValue(parameter.GetValue() ?? string.Empty);
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
