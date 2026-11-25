using Autodesk.Revit.DB;
using BIMPlugins.Bars;
using BIMPlugins.Classes;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Sheets.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Sheets.WPF
{
    public enum NumberPosition { Start, End }

    public partial class CopySheetsVM : ObservableObject
    {
        private const string Title = "Дубликатор листов";

        [ObservableProperty] private string _filter;
        [ObservableProperty] private ICollectionView _sheetGroups;

        // The settings of the last window are kept for the next one.
        [ObservableProperty] private static bool _isCopyingViews = false;
        [ObservableProperty] private static bool _isCopyingSchedules = false;
        [ObservableProperty] private static bool _isCopyingLegends = false;
        [ObservableProperty] private static bool _isCopyingTextNotes = false;
        [ObservableProperty] private static bool _isCopyingAnnotations = false;
        [ObservableProperty] private static bool _isCopyingLines = false;
        [ObservableProperty] private static bool _isMakingCopiesForSchedules = false;
        [ObservableProperty] private static bool _isMakingCopiesForLegends = false;
        [ObservableProperty] private static NumberPosition _selectedNumberPosition = NumberPosition.End;

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

        private readonly Document _doc;

        // All sheet numbers of the project, with those of the copies made: a new number must be none of them.
        private readonly HashSet<string> _sheetNumbers;

        partial void OnFilterChanged(string value) => SheetGroups.Refresh();

        partial void OnSelectedViewDuplicateOptionChanged(ViewDuplicateOption value) =>
            IsSelectedAsDependent = value == ViewDuplicateOption.AsDependent;

        public IReadOnlyList<Option<NumberPosition>> NumberPositions { get; } =
        [
            new(NumberPosition.Start, "Начале"),
            new(NumberPosition.End, "Конце")
        ];

        public List<string> ViewDuplicateOptions { get; } = ["Копировать", "Копировать с детализацией", "Создать зависимый вид"];

        public CopySheetsVM(IList<ViewSheet> sheets)
        {
            _doc = RevitAPI.Document;
            _sheetNumbers = sheets.Select(s => s.SheetNumber).ToHashSet();

            // The schedules of each sheet, found once; revision schedules of title blocks are not copied.
            var schedulesBySheet = _doc.ToElements<ScheduleSheetInstance>()
                .Where(s => !s.IsTitleblockRevisionSchedule)
                .ToLookup(s => s.OwnerViewId);

            var sheetItems = sheets.Select(sheet =>
            {
                var sheetItem = new SheetCopyItem(sheet);

                var viewItems = sheet.GetAllViewports()
                    .Select(id => new ViewItem(id.ToElement<Viewport>(_doc)) { Parent = sheetItem })
                    .Concat(schedulesBySheet[sheet.Id]
                        .GroupBy(s => s.ScheduleId)
                        .Select(g => new ViewItem(g.First()) { Parent = sheetItem }));

                sheetItem.ViewItems = viewItems
                    .OrderBy(v => v.Name, NaturalComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return sheetItem;
            }).ToList();

            var groups = Classes.SheetGroups.Create(_doc, sheetItems);
            SheetGroups = Classes.SheetGroups.CreateView(groups, () => Filter);
        }

        [RelayCommand]
        private void Run()
        {
            var selectedSheetItems = SheetGroups
                .Cast<GroupItem>()
                .SelectMany(g => g.Sheets.Cast<SheetCopyItem>())
                .Where(s => s.IsSelected == true)
                .ToList();

            if (selectedSheetItems.Count == 0)
            {
                MessageWindow.ShowMessage("Не выбраны листы для копирования!", Title, icon: MessageBoxImage.Warning);
                return;
            }

            RaiseCloseRequest();

            // What could not be copied is told at the end instead of being lost.
            var errors = new List<string>();

            using (Transaction t = new Transaction(_doc, Title))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Копирование листов...", selectedSheetItems, sheetItem =>
                    {
                        var sourceSheet = sheetItem.Element;
                        var titleBlocks = _doc.ToElements(sourceSheet.Id, BuiltInCategory.OST_TitleBlocks);

                        if (titleBlocks.Count == 0)
                        {
                            errors.Add($"{sourceSheet.SheetNumber}: на листе нет штампа, лист не скопирован");
                            return;
                        }

                        var position = 1;
                        for (var i = 0; i < sheetItem.CopiesAmount; i++)
                        {
                            var newSheet = ViewSheet.Create(_doc, titleBlocks[0].GetTypeId());

                            string number;
                            while (_sheetNumbers.Contains(number = ComposeNumber(sourceSheet.SheetNumber, position)))
                                position++;
                            position++;

                            _sheetNumbers.Add(number);
                            newSheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).Set(number);

                            // The title block of a new sheet stands at the origin; it is moved where the source one stands.
                            var newTitleBlock = _doc.ToElements(newSheet.Id, BuiltInCategory.OST_TitleBlocks).First();
                            newTitleBlock.Move(titleBlocks[0].ToPoint() - newTitleBlock.ToPoint());

                            CopyParameters(sourceSheet, newSheet);
                            if (IsCopyingTitleBlockParameters)
                                CopyParameters(titleBlocks[0], newTitleBlock);

                            foreach (var titleBlock in titleBlocks.Skip(1))
                            {
                                newTitleBlock = _doc.Create.NewFamilyInstance(titleBlock.ToPoint(), titleBlock.ToElementType<FamilySymbol>(), newSheet);

                                if (IsCopyingTitleBlockParameters)
                                    CopyParameters(titleBlock, newTitleBlock);
                            }

                            if (IsCopyingViews) CopyViews(sheetItem, newSheet, errors);
                            if (IsCopyingSchedules) CopySchedules(sheetItem, newSheet, errors);
                            if (IsCopyingLegends) CopyLegends(sheetItem, newSheet, errors);
                            if (IsCopyingAnnotations) CopyElements(sheetItem, newSheet, _doc.ToElementIds(sourceSheet.Id, BuiltInCategory.OST_GenericAnnotation), "аннотации", errors);
                            if (IsCopyingTextNotes) CopyElements(sheetItem, newSheet, _doc.ToElementIds(sourceSheet.Id, BuiltInCategory.OST_TextNotes), "текст", errors);
                            if (IsCopyingLines) CopyElements(sheetItem, newSheet, _doc.ToElementIds<CurveElement>(sourceSheet.Id), "линии", errors);

                            newSheet.Name = NamePrefix + (sheetItem.NewCopyName.IsNullOrEmpty() ? newSheet.Name : sheetItem.NewCopyName) + NameSuffix;

                            _doc.Regenerate();
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

            if (errors.Count != 0)
                ReportWindow.ShowReport(errors, "Не всё удалось скопировать");
        }

        // The position goes before or after the number of the source sheet; "$" sets it apart until the sheets are renumbered.
        private string ComposeNumber(string sourceNumber, int position) => SelectedNumberPosition == NumberPosition.Start
            ? $"{NumberPrefix}{position}${sourceNumber}{NumberSuffix}"
            : $"{NumberPrefix}{sourceNumber}${position}{NumberSuffix}";

        // Parameters of a sheet, or of a title block of the same family, are found by id: the same parameter whatever its name and language.
        private static void CopyParameters(Element source, Element target)
        {
            var sheetNumberId = new ElementId(BuiltInParameter.SHEET_NUMBER);

            foreach (var sourceParam in source.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && p.HasValue && p.Id != sheetNumberId))
            {
                var targetParam = target.ToParameter(sourceParam.Id);
                if (targetParam == null || targetParam.IsReadOnly)
                    continue;

                try
                {
                    targetParam.SetValue(sourceParam.GetValue());
                }
                catch { } // A parameter Revit does not let set here, such as one driven by another, stays as it is.
            }
        }

        private void CopyViews(SheetCopyItem sheetItem, ViewSheet targetSheet, List<string> errors)
        {
            foreach (var viewItem in sheetItem.ViewItems.Where(v => v.IsSelected && v.ViewType != ViewType.Legend && v.ViewType != ViewType.Schedule))
            {
                try
                {
                    // A drafting view has no dependent views: it is always copied with its detailing.
                    var option = viewItem.ViewType == ViewType.DraftingView ? ViewDuplicateOption.WithDetailing : SelectedViewDuplicateOption;
                    var newViewId = viewItem.View.Duplicate(option);

                    if (option == ViewDuplicateOption.AsDependent)
                    {
                        var newView = newViewId.ToElement<View>(_doc);
                        newView.CropBoxActive = CropBoxActive;
                        newView.CropBoxVisible = CropBoxVisible;
                        newView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE).Set(CropAnnotation ? 1 : 0);
                    }

                    Viewport.Create(_doc, targetSheet.Id, newViewId, viewItem.Viewport.GetBoxCenter());
                }
                catch (Exception ex)
                {
                    errors.Add($"{sheetItem.Number}: {viewItem.Name} — {ex.Message}");
                }
            }
        }

        private void CopySchedules(SheetCopyItem sheetItem, ViewSheet targetSheet, List<string> errors)
        {
            foreach (var viewItem in sheetItem.ViewItems.Where(v => v.IsSelected && v.ViewType == ViewType.Schedule))
            {
                try
                {
                    var scheduleId = viewItem.ScheduleSheetInstance.ScheduleId;
                    if (IsMakingCopiesForSchedules)
                        scheduleId = scheduleId.ToElement<View>(_doc).Duplicate(ViewDuplicateOption.Duplicate);

                    ScheduleSheetInstance.Create(_doc, targetSheet.Id, scheduleId, viewItem.CenterPoint);
                }
                catch (Exception ex)
                {
                    errors.Add($"{sheetItem.Number}: {viewItem.Name} — {ex.Message}");
                }
            }
        }

        private void CopyLegends(SheetCopyItem sheetItem, ViewSheet targetSheet, List<string> errors)
        {
            foreach (var viewItem in sheetItem.ViewItems.Where(v => v.IsSelected && v.ViewType == ViewType.Legend))
            {
                try
                {
                    var legendId = IsMakingCopiesForLegends
                        ? viewItem.View.Duplicate(ViewDuplicateOption.WithDetailing)
                        : viewItem.View.Id;

                    Viewport.Create(_doc, targetSheet.Id, legendId, viewItem.Viewport.GetBoxCenter());
                }
                catch (Exception ex)
                {
                    errors.Add($"{sheetItem.Number}: {viewItem.Name} — {ex.Message}");
                }
            }
        }

        private static void CopyElements(SheetCopyItem sheetItem, ViewSheet targetSheet, ICollection<ElementId> elementIds, string what, List<string> errors)
        {
            if (elementIds.Count == 0)
                return;

            try
            {
                elementIds.CopyElements(sheetItem.Element, targetSheet);
            }
            catch (Exception ex)
            {
                errors.Add($"{sheetItem.Number}: {what} — {ex.Message}");
            }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
