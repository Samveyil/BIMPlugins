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
    public partial class CopySheetsViewModel : ObservableObject
    {
        [ObservableProperty] private string _filter;
        [ObservableProperty] private ICollectionView _sheetGroups;

        [ObservableProperty] private static bool _isCopyingViews = false;
        [ObservableProperty] private static bool _isCopyingSchedules = false;
        [ObservableProperty] private static bool _isCopyingLegends = false;
        [ObservableProperty] private static bool _isCopyingTextNotes = false;
        [ObservableProperty] private static bool _isCopyingAnnotations = false;
        [ObservableProperty] private static bool _isCopyingLines = false;
        [ObservableProperty] private static bool _isMakingCopiesForSchedules = false;
        [ObservableProperty] private static bool _isMakingCopiesForLegends = false;
        [ObservableProperty] private static string _selectedNumberPosition = "Конце";

        [ObservableProperty] private static bool _isCopyingTitleBlockParameters = true;
        [ObservableProperty] private string _numberPrefix;
        [ObservableProperty] private string _numberSuffix;
        [ObservableProperty] private string _namePrefix;
        [ObservableProperty] private string _nameSuffix;

        [ObservableProperty] private static ViewDuplicateOption _selectedViewDuplicateOption = ViewDuplicateOption.WithDetailing;
        [ObservableProperty] private static bool _cropBoxActive = true;
        [ObservableProperty] private static bool _cropBoxVisible = true;
        [ObservableProperty] private static bool _cropAnnotation = true;
        [ObservableProperty] private static bool _isSelectedAsDependent = false;

        partial void OnFilterChanged(string value)
        {
            SheetGroups.Refresh();
        }
        partial void OnSelectedViewDuplicateOptionChanged(ViewDuplicateOption value)
        {
            IsSelectedAsDependent = value == ViewDuplicateOption.AsDependent;
        }

        private List<string> SheetsNumbersList { get; set; } = [];
        public List<string> NumberPositions { get; set; } = ["Начале", "Конце"];
        public List<string> ViewDuplicateOptions { get; set; } = ["Копировать", "Копировать с детализацией", "Создать зависимый вид"];

        public CopySheetsViewModel(IList<Element> sheets)
        {
            var scheduleGraphics = new FilteredElementCollector(RevitAPI.Document)
                .OfCategory(BuiltInCategory.OST_ScheduleGraphics)
                .WhereElementIsNotElementType()
                .Where(s => !s.Name.Contains("Ведомость изменений"))
                .Cast<ScheduleSheetInstance>()
                .ToList();

            var bo = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(RevitAPI.Document);
            
            FolderItemInfo folderItemInfo;
            ElementId paramId;

            try
            {
                folderItemInfo = bo.GetFolderItems(sheets[0].Id)[0];
                
                paramId = folderItemInfo.ElementId;
            }
            catch { folderItemInfo = null; paramId = ElementId.InvalidElementId; }

            var sheetListItems = new List<SheetCopyItem>();
            var sections = new HashSet<string>();

            foreach (var sheet in sheets)
            {
                var sheetItem = new SheetCopyItem((ViewSheet)sheet);

                SheetsNumbersList.Add(sheetItem.Number);

                if (folderItemInfo != null)
                {
                    var sheetParameter = sheet.ToParameter(paramId);
                    sheetItem.GroupItemName = sheetParameter.AsString().IsNullOrEmpty()
                        ? ""
                        : sheetParameter.AsString();
                }

                foreach (var elemId in (sheet as ViewSheet).GetAllViewports())
                {
                    var viewport = elemId.ToElement<Viewport>(); 
                    var view = viewport.ViewId.ToElement<View>();
                    
                    var viewItem = new ViewItem(viewport)
                    {
                        Parent = sheetItem,
                    };

                    sheetItem.ViewItems.Add(viewItem);
                }

                string sheetId = sheet.Id.ToString();
                foreach (var scheduleGraphic in scheduleGraphics)
                {
                    if (scheduleGraphic.OwnerViewId.ToString() == sheetId &&
                        !sheetItem.ViewItems.Select(i => i.ScheduleSheetInstance?.ScheduleId.ToString()).Contains(scheduleGraphic.ScheduleId.ToString()))
                    {
                        var viewItem = new ViewItem(scheduleGraphic)
                        {
                            Parent = sheetItem,
                        };
                        
                        sheetItem.ViewItems.Add(viewItem);
                    }
                }

                sheetItem.ViewItems = sheetItem.ViewItems
                    .OrderBy(s => s.Name, new NaturalComparer())
                    .ToList();

                sections.Add(sheetItem.GroupItemName);
                sheetListItems.Add(sheetItem);
            }

            var sheetGroups = new ObservableCollection<GroupItem>();
            foreach (var section in sections)
            {
                List<SheetCopyItem> subList = [];
                foreach (var sheetListItem in sheetListItems.OrderBy(s => s.Number, new NaturalComparer()))
                {
                    if (sheetListItem.GroupItemName == section) subList.Add(sheetListItem);
                }

                if (subList.Count > 0)
                {
                    var groupSheets = new GroupItem
                    {
                        Name = section.IsNullOrEmpty() ? "???" : section,
                        Sheets = CollectionViewSource.GetDefaultView(subList)
                    };

                    subList.ForEach(v => v.Parent = groupSheets);

                    groupSheets.Sheets.Filter = item =>
                    {
                        if (Filter.IsNullOrEmpty())
                            return true;

                        var sheetItem = (SheetCopyItem)item;
                        return sheetItem.Title.Contains(Filter, StringComparison.OrdinalIgnoreCase);
                    };

                    sheetGroups.Add(groupSheets);
                }
            }

            if (sheetGroups.Count == 1 && sheetGroups.First().Name == "???")
                sheetGroups.First().Name = "все";

            sheetGroups = new(sheetGroups.OrderBy(s => s.Name, new NaturalComparer()).ToList());

            SheetGroups = CollectionViewSource.GetDefaultView(sheetGroups);
            SheetGroups.Filter = item =>
            {
                var groupSheet = (GroupItem)item;
                groupSheet.Sheets.Refresh();

                if (Filter.IsNullOrEmpty())
                    return true;

                return groupSheet.Sheets.Cast<SheetCopyItem>().Count() != 0;
            };
        }

        [RelayCommand]
        private void Run()
        {
            var selectedSheetItems = SheetGroups
                .Cast<GroupItem>()
                .SelectMany(s => s.Sheets.Cast<SheetCopyItem>())
                .Where(s => s.IsSelected == true)
                .ToList();

            if (selectedSheetItems.Count == 0)
            {
                MessageBox.Show("Не выбраны листы для копирования!", "SE Plugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RaiseCloseRequest();

            using (Transaction t = new Transaction(RevitAPI.Document, "Дубликатор листов"))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Копирование листов...", selectedSheetItems, (sheetItem) =>
                    {
                        var titleBlocks = new FilteredElementCollector(RevitAPI.Document, sheetItem.Element.Id)
                            .OfCategory(BuiltInCategory.OST_TitleBlocks)
                            .WhereElementIsNotElementType()
                            .ToList();

                        int position = 1;
                        for (int i = 1; i < sheetItem.CopiesAmount + 1; i++)
                        {
                            var newSheet = ViewSheet.Create(RevitAPI.Document, titleBlocks[0].GetTypeId());

                            var uniqueNumber = string.Empty;
                            if (SelectedNumberPosition == "Начале")
                            {
                                string newNumber = NumberPrefix + position + '$' + sheetItem.Element.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString() + NumberSuffix;

                                (uniqueNumber, position) = CheckNewNumberAvailability(newNumber, position);
                                SheetsNumbersList.Add(uniqueNumber);
                            }
                            else
                            {
                                string newNumber = NumberPrefix + sheetItem.Element.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString() + '$' + position + NumberSuffix;

                                (uniqueNumber, position) = CheckNewNumberAvailability(newNumber, position);
                                SheetsNumbersList.Add(uniqueNumber);
                            }
                            position++;

                            newSheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).Set(uniqueNumber);
                            
                            var newTitleBlock = new FilteredElementCollector(RevitAPI.Document, newSheet.Id)
                                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                                .WhereElementIsNotElementType()
                                .FirstOrDefault();
                            
                            ElementTransformUtils.MoveElement(RevitAPI.Document, newTitleBlock.Id, (titleBlocks[0].Location as LocationPoint).Point);
                            
                            CopySheetParameters(sheetItem.Element, newSheet);

                            if (IsCopyingTitleBlockParameters) CopyTitleBlockParameters(titleBlocks[0], newTitleBlock);

                            if (titleBlocks.Count > 1)
                            {
                                foreach ( var titleBlock in titleBlocks.Skip(1))
                                {
                                    newTitleBlock = RevitAPI.Document.Create.NewFamilyInstance(
                                        (titleBlock.Location as LocationPoint).Point,
                                        titleBlock.ToElementType() as FamilySymbol,
                                        newSheet);

                                    if (IsCopyingTitleBlockParameters) CopyTitleBlockParameters(titleBlock, newTitleBlock);
                                }
                            }
                            
                            if (IsCopyingViews) CopyViews(sheetItem, newSheet);
                            if (IsCopyingSchedules) CopySchedules(sheetItem, newSheet);
                            if (IsCopyingLegends) CopyLegends(sheetItem, newSheet);
                            if (IsCopyingAnnotations) CopyAnnotations(sheetItem, newSheet);
                            if (IsCopyingTextNotes) CopyTextNotes(sheetItem, newSheet);
                            if (IsCopyingLines) CopyLines(sheetItem, newSheet);

                            newSheet.Name = sheetItem.NewCopyName.IsNullOrEmpty()
                                ? NamePrefix + newSheet.Name + NameSuffix
                                : NamePrefix + sheetItem.NewCopyName + NameSuffix;

                            RevitAPI.Document.Regenerate();
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

        private (string, int) CheckNewNumberAvailability(string number, int position)
        {
            string currentNumber = number;
            int currentAmount = position;
            int maxAttempts = 400;
            int attempts = 0;

            while (SheetsNumbersList.Contains(currentNumber) && attempts < maxAttempts)
            {
                currentNumber = SelectedNumberPosition == "Начале"
                    ? currentNumber.Replace(currentAmount.ToString() + '$', (currentAmount + 1).ToString() + '$')
                    : currentNumber.Replace('$' + currentAmount.ToString(), '$' + (currentAmount + 1).ToString());

                currentAmount++;
                attempts++;
            }

            return (currentNumber, currentAmount);
        }

        private void CopySheetParameters(ViewSheet sourceSheet, ViewSheet targetSheet)
        {
            foreach (Parameter sourceParam in sourceSheet.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && p.HasValue).ToList())
            {
                Parameter targetParam = targetSheet.LookupParameter(sourceParam.Definition.Name);
                if (targetParam != null && !targetParam.IsReadOnly && targetParam.Definition.Name != "Номер листа")
                {
                    try
                    {
                        targetParam.SetValue(sourceParam.GetValue());
                    }
                    catch { }
                }
            }
        }
        private void CopyTitleBlockParameters(Element sourceTitleBlock, Element targetTitleBlock)
        {
            foreach (Parameter sourceParam in sourceTitleBlock.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && p.HasValue).ToList())
            {
                Parameter targetParam = targetTitleBlock.LookupParameter(sourceParam.Definition.Name);
                if (targetParam != null && !targetParam.IsReadOnly && targetParam.Definition.Name != "Номер листа")
                {
                    try
                    {
                        targetParam.SetValue(sourceParam.GetValue());
                    }
                    catch { }
                }
            }
        }
        private void CopyViews(SheetCopyItem sheetItem, ViewSheet targetSheet)
        {
            foreach (ViewItem viewItem in sheetItem.ViewItems
                    .Where(v => v.IsSelected && v.ViewType != ViewType.Legend && v.ViewType != ViewType.Schedule)
                    .ToList())
            {
                try
                {
                    ElementId newViewId = null;
                    View newView = null;
                    if (viewItem.ViewType == ViewType.DraftingView)
                    {
                        newViewId = viewItem.View.Duplicate(ViewDuplicateOption.WithDetailing);
                        newView = newViewId.ToElement<View>();
                    }
                    else
                    {
                        newViewId = viewItem.View.Duplicate(SelectedViewDuplicateOption);
                        newView = newViewId.ToElement<View>();
                        if (SelectedViewDuplicateOption == ViewDuplicateOption.AsDependent)
                        {
                            newView.CropBoxActive = CropBoxActive;
                            newView.CropBoxVisible = CropBoxVisible;
                            newView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE).Set(CropAnnotation ? 1 : 0);
                        }
                    }
                    
                    Viewport.Create(RevitAPI.Document, targetSheet.Id, newViewId, viewItem.Viewport.GetBoxCenter());
                }
                catch { }
            }
        }
        private void CopySchedules(SheetCopyItem sheetItem, ViewSheet targetSheet)
        {
            foreach (ViewItem viewItem in sheetItem.ViewItems
                    .Where(v => v.IsSelected && v.ViewType == ViewType.Schedule)
                    .ToList())
            {
                if (IsMakingCopiesForSchedules)
                {
                    try
                    {
                        var schedule = viewItem.ScheduleSheetInstance.ScheduleId.ToElement<View>();
                        var newScheduleId = schedule.Duplicate(ViewDuplicateOption.Duplicate);

                        ScheduleSheetInstance.Create(RevitAPI.Document, targetSheet.Id, newScheduleId, viewItem.CenterPoint);
                    }
                    catch { }
                }
                else
                {
                    ScheduleSheetInstance.Create(RevitAPI.Document, targetSheet.Id, viewItem.ScheduleSheetInstance.ScheduleId, viewItem.CenterPoint);
                }
            }
        }
        private void CopyLegends(SheetCopyItem sheetItem, ViewSheet targetSheet)
        {
            foreach (ViewItem viewItem in sheetItem.ViewItems
                    .Where(v => v.IsSelected && v.ViewType == ViewType.Legend)
                    .ToList())
            {
                if (IsMakingCopiesForLegends)
                {
                    try
                    {
                        var newLegendId = viewItem.View.Duplicate(ViewDuplicateOption.WithDetailing);
                        Viewport.Create(RevitAPI.Document, targetSheet.Id, newLegendId, viewItem.Viewport.GetBoxCenter());
                    }
                    catch { }
                }
                else
                {
                    Viewport.Create(RevitAPI.Document, targetSheet.Id, viewItem.View.Id, viewItem.Viewport.GetBoxCenter());
                }
            }
        }
        private void CopyAnnotations(SheetCopyItem sheetItem, ViewSheet targetSheet)
        {
            try
            {
                var annotationIds = new FilteredElementCollector(RevitAPI.Document, sheetItem.Element.Id)
                    .OfCategory(BuiltInCategory.OST_GenericAnnotation)
                    .WhereElementIsNotElementType()
                    .ToElementIds();
                
                ElementTransformUtils.CopyElements(sheetItem.Element, annotationIds, targetSheet, null, new CopyPasteOptions());
            }
            catch { }
        }
        private void CopyTextNotes(SheetCopyItem sheetItem, ViewSheet targetSheet)
        {
            try
            {
                var textNotesIds = new FilteredElementCollector(RevitAPI.Document, sheetItem.Element.Id)
                    .OfCategory(BuiltInCategory.OST_TextNotes)
                    .WhereElementIsNotElementType()
                    .ToElementIds();
                
                ElementTransformUtils.CopyElements(sheetItem.Element, textNotesIds, targetSheet, null, new CopyPasteOptions());
            }
            catch { }
        }
        private void CopyLines(SheetCopyItem sheetItem, ViewSheet targetSheet)
        {
            try
            {
                var lineIds = new FilteredElementCollector(RevitAPI.Document, sheetItem.Element.Id)
                    .OfClass(typeof(CurveElement))
                    .WhereElementIsNotElementType()
                    .ToElementIds();
                
                ElementTransformUtils.CopyElements(sheetItem.Element, lineIds, targetSheet, null, new CopyPasteOptions());
            }
            catch { }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}