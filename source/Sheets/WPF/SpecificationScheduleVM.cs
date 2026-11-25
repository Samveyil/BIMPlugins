using Aspose.Cells;
using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Sheets.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace BIMPlugins.Sheets.WPF
{
    public partial class SpecificationScheduleVM : ObservableObject
    {
        private const string Title = "Ведомость спецификаций";
        private const string SchedulePrefix = "BIMPlugins_";

        // The shared parameter "Номер листа" of ADSK, chosen by default as the number shown in the list.
        private static readonly Guid SheetNumberParameterGuid = new("b4e34c05-d510-468f-bd86-e753486c8add");

        // Line styles of the ADSK template for the borders of the list.
        private const string BoldLineStyleName = "ADSK_Спецификация толстая";
        private const string ThinLineStyleName = "ADSK_Спецификация тонкая";

        [ObservableProperty] private string _filter;
        [ObservableProperty] private ICollectionView _sheetGroups;

        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private string _newSpecificationName = "Ведомость спецификаций";
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private bool _refreshExistOne = false;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private ViewSchedule _selectedSpecification;
        [ObservableProperty] private Parameter _selectedSheetNumberParameter;
        [ObservableProperty] private static string _selectedFontName = "GOST Common";
        [ObservableProperty] private static bool _isItalic = false;
        [ObservableProperty] private bool _useExcel = false;
        [ObservableProperty] private string _excelFilePath;
        [ObservableProperty] private bool _useTitle = false;
        [ObservableProperty] private ObservableCollection<SpecificationItem> _specificationItems = [];

        private readonly Document _doc;
        private readonly List<SheetScheduleItem> _sheetItems;
        private readonly List<SpecificationItem> _excelItems = [];
        private readonly IList<ViewSheet> _sheets;

        public List<ViewSchedule> CreatedSpecifications { get; }
        public List<Parameter> SheetParameters { get; }
        public List<string> FontNames { get; } = ["Arial", "GOST Common", "ISOCPEUR"];

        partial void OnFilterChanged(string value) => SheetGroups.Refresh();

        partial void OnSelectedSheetNumberParameterChanged(Parameter value)
        {
            if (value == null)
                return;

            _sheetItems.ForEach(sh => sh.NumberFromParameter = GetSheetNumber(sh.Element));

            // Rows from Excel take the number from the same parameter as the others.
            foreach (var specItem in SpecificationItems)
            {
                specItem.SheetNumber = specItem.IsFromExcel
                    ? GetSheetNumber(_sheets.FirstOrDefault(s => s.SheetNumber == specItem.SheetNumberFromExcel))
                    : specItem.ScheduleInstance.Parent.NumberFromParameter;
            }
        }

        partial void OnExcelFilePathChanged(string value)
        {
            if (value.IsNullOrEmpty())
                return;

            foreach (var excelItem in _excelItems)
                SpecificationItems.Remove(excelItem);
            _excelItems.Clear();

            Worksheet worksheet;
            try
            {
                worksheet = new Workbook(value).Worksheets[0];
            }
            catch (Exception ex)
            {
                MessageDialog.Show($"Не удалось прочитать файл Excel. Если он открыт в Excel, закройте его.\n\n{ex.Message}", Title, icon: MessageIcon.Warning);
                return;
            }

            // Rows of sheets the project does not have are told once, all together.
            var missingNumbers = new List<string>();
            for (int row = 0; row <= worksheet.Cells.MaxDataRow; row++)
            {
                var sheetNumber = worksheet.Cells[row, 0].StringValue;
                var sheet = _sheets.FirstOrDefault(s => s.SheetNumber == sheetNumber);
                if (sheet == null)
                {
                    missingNumbers.Add(sheetNumber);
                    continue;
                }

                var specItem = new SpecificationItem
                {
                    RevitName = "Файл Excel",
                    SheetNumberFromExcel = sheetNumber,
                    SheetNumber = GetSheetNumber(sheet),
                    Title = worksheet.Cells[row, 1].StringValue
                };

                _excelItems.Add(specItem);
                SpecificationItems.Add(specItem);
            }

            Sort();

            if (missingNumbers.Count != 0)
                MessageDialog.Show($"В проекте нет листов с номерами: {string.Join(", ", missingNumbers)}", Title, icon: MessageIcon.Warning);
        }

        partial void OnUseExcelChanged(bool value)
        {
            foreach (var excelItem in _excelItems)
                SpecificationItems.Remove(excelItem);

            if (value && !ExcelFilePath.IsNullOrEmpty())
            {
                foreach (var excelItem in _excelItems)
                    SpecificationItems.Add(excelItem);

                Sort();
            }
        }

        public SpecificationScheduleVM(IList<ViewSheet> sheets)
        {
            _doc = RevitAPI.Document;
            _sheets = sheets;

            CreatedSpecifications = _doc.ToElements<ViewSchedule>()
                .Where(sc => sc.Name.Contains("BIMPlugins"))
                .ToList();

            SheetParameters = _sheets[0].Parameters
                .Cast<Parameter>()
                .Where(p => !p.IsReadOnly && p.StorageType == StorageType.String)
                .OrderBy(p => p.Definition.Name)
                .ToList();

            _selectedSheetNumberParameter = SheetParameters.FirstOrDefault(p => p.IsShared && p.GUID == SheetNumberParameterGuid)
                ?? SheetParameters.FirstOrDefault();

            // The schedules of each sheet, found once; revision schedules of title blocks are not listed.
            var schedulesBySheet = _doc.ToElements<ScheduleSheetInstance>()
                .Where(s => !s.IsTitleblockRevisionSchedule)
                .ToLookup(s => s.OwnerViewId);

            _sheetItems = _sheets
                .Where(sheet => schedulesBySheet.Contains(sheet.Id))
                .Select(sheet =>
                {
                    var sheetItem = new SheetScheduleItem(sheet) { NumberFromParameter = GetSheetNumber(sheet) };

                    sheetItem.ScheduleItems = schedulesBySheet[sheet.Id]
                        .Select(s => new ScheduleItem(this)
                        {
                            ScheduleName = s.Name,
                            Element = s.ScheduleId.ToElement<ViewSchedule>(_doc),
                            Parent = sheetItem
                        })
                        .OrderBy(s => s.Name, NaturalComparer.CurrentCultureIgnoreCase)
                        .ToList();

                    return sheetItem;
                })
                .ToList();

            var groups = Classes.SheetGroups.Create(_doc, _sheetItems);
            SheetGroups = Classes.SheetGroups.CreateView(groups, () => Filter);
        }

        // The number of the sheet as the chosen parameter gives it.
        private string GetSheetNumber(ViewSheet sheet) =>
            sheet == null || SelectedSheetNumberParameter == null ? null : sheet.ToParameter(SelectedSheetNumberParameter.Id)?.AsString();

        [RelayCommand]
        private void CollapseAll() => Classes.SheetGroups.CollapseAll(SheetGroups.SourceCollection);

        [RelayCommand]
        private void SelectExcelFile()
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = "*.xlsx",
                Title = "Выберите файл Excel",
                Filter = "файл Excel|*.xlsx"
            };

            if (dialog.ShowDialog() == true)
                ExcelFilePath = dialog.FileName;
        }

        [RelayCommand]
        private void Sort()
        {
            SpecificationItems = new(SpecificationItems
                .OrderBy(s => s.SheetNumber, NaturalComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Title, NaturalComparer.CurrentCultureIgnoreCase));
        }

        [RelayCommand(CanExecute = nameof(IsEnabled))]
        private void Run()
        {
            if (!RefreshExistOne && CreatedSpecifications.Any(sc => sc.Name == SchedulePrefix + NewSpecificationName))
            {
                MessageDialog.Show("Введите уникальное имя ведомости!", Title, icon: MessageIcon.Warning);
                return;
            }

            RaiseCloseRequest();

            var excelSpecifications = SpecificationItems.Where(s => s.IsFromExcel).ToList();
            if (excelSpecifications.Count != 0)
            {
                var renamed = new List<string>();

                using (Transaction t = new Transaction(_doc, "Генерация спецификаций из Excel"))
                {
                    t.Start();

                    foreach (var specItem in excelSpecifications)
                    {
                        var sheet = _sheets.FirstOrDefault(sh => sh.SheetNumber == specItem.SheetNumberFromExcel);
                        if (sheet == null)
                            continue;

                        var schedule = CreateNewExcelSpecification(specItem.Title, renamed);
                        ScheduleSheetInstance.Create(_doc, sheet.Id, schedule.Id, XYZ.Zero);
                    }

                    t.Commit();
                }

                if (renamed.Count != 0)
                    MessageDialog.Show($"Спецификации с этими именами уже есть в проекте, новые получили имена по умолчанию:\n{string.Join("\n", renamed)}",
                        Title, icon: MessageIcon.Warning);
            }

            ViewSchedule viewSchedule;

            using (Transaction t = new Transaction(_doc, Title))
            {
                t.Start();

                if (RefreshExistOne)
                {
                    RefreshSpecification();
                    viewSchedule = SelectedSpecification;
                }
                else
                {
                    viewSchedule = CreateNewSpecification();
                }

                t.Commit();
            }

            RevitAPI.UIDocument.ActiveView = viewSchedule;
        }

        private bool IsEnabled() => RefreshExistOne
            ? SelectedSpecification != null
            : !NewSpecificationName.IsNullOrEmpty();

        [RelayCommand]
        private void RemoveItem(SpecificationItem specificationItem)
        {
            if (specificationItem.IsFromExcel)
                SpecificationItems.Remove(specificationItem);
            else
                specificationItem.ScheduleInstance.IsSelected = false;
        }

        // The line styles of the borders; null for a style the project does not have.
        private (GraphicsStyle Bold, GraphicsStyle Thin) GetLineStyles()
        {
            var styles = _doc.ToElements<GraphicsStyle>();
            return (styles.FirstOrDefault(s => s.Name == BoldLineStyleName), styles.FirstOrDefault(s => s.Name == ThinLineStyleName));
        }

        // A row is 8 mm high, or 12 mm for a title too long for one line of the 140 mm column. Titles are measured as text
        // notes, all of them at once, with a single regeneration; the notes are deleted right after.
        private List<double> MeasureRowHeights(List<SpecificationItem> items, ElementId textNoteType)
        {
            var columnWidth = 140d.FromMillimeters();
            var temporarySheetId = _sheets[0].Id;

            // A text note cannot be empty; an empty title takes one line anyway.
            var notes = items.Select(i => TextNote.Create(_doc, temporarySheetId, XYZ.Zero, i.Title.IsNullOrWhiteSpace() ? " " : i.Title, textNoteType)).ToList();
            _doc.Regenerate();

            var heights = notes.Select(n => (n.Width > columnWidth ? 12d : 8d).FromMillimeters()).ToList();
            _doc.Delete(notes.Select(n => n.Id).ToList());

            return heights;
        }

        private TableCellStyle GetTableCellStyle(Element boldLine, Element thinLine)
        {
            var cellStyle = new TableCellStyle();

            var overrideOptions = cellStyle.GetCellStyleOverrideOptions();
            overrideOptions.FontSize = true;
            overrideOptions.Italics = IsItalic;
            overrideOptions.Font = true;
            overrideOptions.HorizontalAlignment = true;
            overrideOptions.VerticalAlignment = true;
            overrideOptions.BorderTopLineStyle = true;
            overrideOptions.BorderBottomLineStyle = true;
            overrideOptions.BorderRightLineStyle = true;
            overrideOptions.BorderLeftLineStyle = true;

            cellStyle.TextSize = 3.0 * 96 / 25.4;       // 3 мм в пиксели
            cellStyle.IsFontItalic = true;
            cellStyle.FontName = SelectedFontName;
            cellStyle.FontHorizontalAlignment = HorizontalAlignmentStyle.Center;
            cellStyle.FontVerticalAlignment = VerticalAlignmentStyle.Middle;

            if (boldLine != null)
            {
                cellStyle.BorderLeftLineStyle = boldLine.Id;
                cellStyle.BorderRightLineStyle = boldLine.Id;
            }
            if (thinLine != null)
            {
                cellStyle.BorderTopLineStyle = thinLine.Id;
                cellStyle.BorderBottomLineStyle = thinLine.Id;
            }

            cellStyle.SetCellStyleOverrideOptions(overrideOptions);

            return cellStyle;
        }

        private ViewSchedule CreateNewExcelSpecification(string name, List<string> renamed)
        {
            var schedule = ViewSchedule.CreateSchedule(_doc, ElementId.InvalidElementId);
            try
            {
                schedule.Name = name;
            }
            catch
            {
                renamed.Add(name);
            }

            var definition = schedule.Definition;
            definition.ShowHeaders = false;

            var markField = definition.AddField(ScheduleFieldType.Instance, new ElementId(BuiltInParameter.DOOR_NUMBER));
            definition.AddFilter(new ScheduleFilter(markField.FieldId, ScheduleFilterType.Equal, "BIMPlugins"));

            return schedule;
        }

        private ViewSchedule CreateNewSpecification()
        {
            var intUnit = 1d.FromMillimeters();

            var schedule = ViewSchedule.CreateSchedule(_doc, ElementId.InvalidElementId);
            schedule.Name = SchedulePrefix + NewSpecificationName;

            var definition = schedule.Definition;
            definition.ShowHeaders = false;

            var markField = definition.AddField(ScheduleFieldType.Instance, new ElementId(BuiltInParameter.DOOR_NUMBER));
            definition.AddFilter(new ScheduleFilter(markField.FieldId, ScheduleFilterType.Equal, "BIMPlugins"));

            var tableData = schedule.GetTableData();
            var bodySectionData = tableData.GetSectionData(SectionType.Body);
            var headerSectionData = tableData.GetSectionData(SectionType.Header);

            bodySectionData.SetColumnWidth(0, 185 * intUnit);

            headerSectionData.ClearCell(0, 0);

            for (var i = 0; i < 2; i++)
                headerSectionData.InsertColumn(0);

            headerSectionData.SetColumnWidth(0, 15 * intUnit);
            headerSectionData.SetColumnWidth(1, 140 * intUnit);
            headerSectionData.SetColumnWidth(2, 30 * intUnit);

            // Rows are inserted at the top, so the items go from the last to the first.
            var items = SpecificationItems
                .OrderByDescending(s => s.SheetNumber, NaturalComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(s => s.Title, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();
            var heights = MeasureRowHeights(items, schedule.TitleTextTypeId);

            for (var i = 0; i < items.Count; i++)
            {
                headerSectionData.SetRowHeight(0, heights[i]);

                try
                {
                    headerSectionData.SetCellText(0, 1, items[i].Title);
                    headerSectionData.SetCellText(0, 0, items[i].SheetNumber);
                }
                catch { } // A text the cell does not take leaves the cell empty.

                headerSectionData.InsertRow(0);
            }

            headerSectionData.SetCellText(0, 0, "Лист");
            headerSectionData.SetCellText(0, 1, "Наименование");
            headerSectionData.SetCellText(0, 2, "Примечание");
            headerSectionData.SetRowHeight(0, 15 * intUnit);

            var (boldLine, thinLine) = GetLineStyles();

            var cellStyle = GetTableCellStyle(boldLine, thinLine);
            headerSectionData.SetCellStyle(cellStyle);

            var middleColumnCellStyle = new TableCellStyle(cellStyle);
            middleColumnCellStyle.FontHorizontalAlignment = HorizontalAlignmentStyle.Left;
            headerSectionData.SetCellStyle(1, middleColumnCellStyle);

            var firstRowCellStyle = new TableCellStyle(cellStyle);
            firstRowCellStyle.BorderTopLineStyle = boldLine?.Id;
            headerSectionData.SetCellStyle(1, 0, firstRowCellStyle);
            headerSectionData.SetCellStyle(1, 2, firstRowCellStyle);

            var firstRowMiddleStyle = new TableCellStyle(middleColumnCellStyle);
            firstRowMiddleStyle.BorderTopLineStyle = boldLine?.Id;
            headerSectionData.SetCellStyle(1, 1, firstRowMiddleStyle);

            var lastRowCellStyle = new TableCellStyle(cellStyle);
            lastRowCellStyle.BorderBottomLineStyle = boldLine?.Id;
            headerSectionData.SetCellStyle(headerSectionData.LastRowNumber, 0, lastRowCellStyle);
            headerSectionData.SetCellStyle(headerSectionData.LastRowNumber, 2, lastRowCellStyle);

            var lastRowMiddleCellStyle = new TableCellStyle(middleColumnCellStyle);
            lastRowMiddleCellStyle.BorderBottomLineStyle = boldLine?.Id;
            headerSectionData.SetCellStyle(headerSectionData.LastRowNumber, 1, lastRowMiddleCellStyle);

            var titleCellStyle = new TableCellStyle(cellStyle);
            titleCellStyle.TextSize = 3.5 * 96 / 25.4;              // 3.5 мм в пиксели
            titleCellStyle.BorderTopLineStyle = boldLine?.Id;
            titleCellStyle.BorderBottomLineStyle = boldLine?.Id;

            headerSectionData.SetCellStyle(0, 0, titleCellStyle);
            headerSectionData.SetCellStyle(0, 1, titleCellStyle);
            headerSectionData.SetCellStyle(0, 2, titleCellStyle);

            headerSectionData.InsertRow(0);

            var mergedCell = new TableMergedCell(0, 0, 0, 2);
            headerSectionData.SetMergedCell(0, 0, mergedCell);
            headerSectionData.SetCellText(0, 0, NewSpecificationName);

            var headerCellStyle = new TableCellStyle(cellStyle);
            headerCellStyle.TextSize = 5 * 96 / 25.4;              // 5 мм в пиксели
            headerCellStyle.BorderTopLineStyle = ElementId.InvalidElementId;
            headerCellStyle.BorderBottomLineStyle = ElementId.InvalidElementId;
            headerCellStyle.BorderLeftLineStyle = ElementId.InvalidElementId;
            headerCellStyle.BorderRightLineStyle = ElementId.InvalidElementId;
            headerSectionData.SetCellStyle(0, 0, headerCellStyle);

            return schedule;
        }

        private void RefreshSpecification()
        {
            var headerSectionData = SelectedSpecification.GetTableData().GetSectionData(SectionType.Header);

            // The list has two rows above its items: the name and the column titles.
            var rowNumbers = headerSectionData.NumberOfRows - 2;
            if (rowNumbers < SpecificationItems.Count)
            {
                var cellStyle = headerSectionData.GetTableCellStyle(rowNumbers, 0);
                var middleCellStyle = headerSectionData.GetTableCellStyle(rowNumbers, 1);

                while (rowNumbers != SpecificationItems.Count)
                {
                    headerSectionData.InsertRow(rowNumbers);
                    headerSectionData.SetCellStyle(rowNumbers, 0, cellStyle);
                    headerSectionData.SetCellStyle(rowNumbers, 1, middleCellStyle);
                    headerSectionData.SetCellStyle(rowNumbers, 2, cellStyle);

                    rowNumbers++;
                }
            }
            else
            {
                while (rowNumbers > SpecificationItems.Count)
                {
                    headerSectionData.RemoveRow(rowNumbers);
                    rowNumbers--;
                }
            }

            var items = SpecificationItems
                .OrderBy(s => s.SheetNumber, NaturalComparer.CurrentCultureIgnoreCase)
                .ThenBy(s => s.Title, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();
            var heights = MeasureRowHeights(items, SelectedSpecification.TitleTextTypeId);

            for (var i = 0; i < items.Count; i++)
            {
                var row = i + 2;
                headerSectionData.SetRowHeight(row, heights[i]);

                try
                {
                    headerSectionData.SetCellText(row, 1, items[i].Title);
                    headerSectionData.SetCellText(row, 0, items[i].SheetNumber);
                }
                catch { } // A text the cell does not take leaves the cell empty.
            }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
