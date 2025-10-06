using Aspose.Cells;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using Microsoft.Win32;
using BIMPlugins.Sheets.Classes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using BIMPlugins.ExtStorage.Methods;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Sheets.WPF
{
    public partial class SpecificationScheduleViewModel : ObservableObject
    {
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

        private readonly ElementId _paramId;
        private readonly List<SheetScheduleItem> _sheetItems = [];
        private readonly List<SpecificationItem> _excelItems = [];
        private readonly List<ViewSheet> _sheets = [];

        public List<ViewSchedule> CreatedSpecifications { get; set; } = [];
        public List<Parameter> SheetParameters { get; set; } = [];
        public List<string> FontNames { get; set; } = ["Arial", "GOST Common", "ISOCPEUR"];

        partial void OnFilterChanged(string value)
        {
            SheetGroups.Refresh();
        }
        partial void OnSelectedSheetNumberParameterChanging(Parameter value)
        {
            _sheetItems.ForEach(sh => sh.NumberFromParameter = sh.Element.ToParameter(value.Id).AsString());

            foreach (var specItem in SpecificationItems.Where(sp => sp.RevitName != "Файл Excel").ToList())
            {
                specItem.SheetNumber = specItem.ScheduleInstance.Parent.NumberFromParameter;
            }
        }
        partial void OnExcelFilePathChanged(string value)
        {
            if (!value.IsNullOrEmpty())
            {
                foreach (var excelItem in _excelItems)
                {
                    SpecificationItems.Remove(excelItem);
                }
                _excelItems.Clear();

                Workbook workbook = new Workbook(value);
                Worksheet worksheet = workbook.Worksheets[0];

                int rows = worksheet.Cells.MaxDataRow;

                for (int row = 0; row <= rows; row++)
                {
                    var specItem = new SpecificationItem()
                    {
                        RevitName = "Файл Excel",
                        SheetNumberFromExcel = worksheet.Cells[row, 0].StringValue,
                        Title = worksheet.Cells[row, 1].StringValue
                    };

                    var sheet = _sheets.FirstOrDefault(s => s.SheetNumber == specItem.SheetNumberFromExcel);
                    if (sheet != null)
                    {
                        specItem.SheetNumber = sheet.ToParameter(SelectedSheetNumberParameter.Id).AsString();
                    }
                    else
                    {
                        MessageBox.Show($"Лист с номером {specItem.SheetNumberFromExcel} не существует в проекте!", "SE Plugins",
                            MessageBoxButton.OK, MessageBoxImage.Warning);

                        continue;
                    }

                    _excelItems.Add(specItem);
                    SpecificationItems.Add(specItem);
                }

                SpecificationItems = new(SpecificationItems
                    .OrderBy(s => s.SheetNumber, new NaturalComparer())
                    .ThenBy(s => s.Title, new NaturalComparer())
                    .ToList());
            }
        }
        partial void OnUseExcelChanged(bool value)
        {
            if (!ExcelFilePath.IsNullOrEmpty() && value)
            {
                foreach (var excelItem in _excelItems)
                {
                    SpecificationItems.Add(excelItem);
                }

                SpecificationItems = new(SpecificationItems
                    .OrderBy(s => s.SheetNumber, new NaturalComparer())
                    .ThenBy(s => s.Title, new NaturalComparer())
                    .ToHashSet()
                    .ToList());
            }
            else
            {
                foreach (var excelItem in _excelItems)
                {
                    SpecificationItems.Remove(excelItem);
                }
            }
        }

        public SpecificationScheduleViewModel(List<ViewSheet> sheets)
        {
            _sheets = sheets;

            CreatedSpecifications = new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(sc => sc.Name.Contains("SE Plugins"))
                .ToList();

            SheetParameters = _sheets[0].Parameters
                .Cast<Parameter>()
                .Where(parameter => !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
                .OrderBy(parameter => parameter.Definition.Name)
                .ToList();

            SelectedSheetNumberParameter = SheetParameters.FirstOrDefault(p => p.IsShared && p.GUID == new Guid("b4e34c05-d510-468f-bd86-e753486c8add")) ?? SheetParameters.First();

            var scheduleGraphics = new FilteredElementCollector(RevitAPI.Document)
                .OfCategory(BuiltInCategory.OST_ScheduleGraphics)
                .WhereElementIsNotElementType()
                .Where(s => !s.Name.Contains("Ведомость изменений"))
                .ToList();

            var bo = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(RevitAPI.Document);
            FolderItemInfo folderItemInfo;

            try
            {
                folderItemInfo = bo.GetFolderItems(sheets[0].Id)[0];
                _paramId = folderItemInfo.ElementId;
            }
            catch { folderItemInfo = null; }

            var sections = new HashSet<string>();
            foreach (var sheet in _sheets)
            {
                var sheetItem = new SheetScheduleItem(sheet);
                sheetItem.NumberFromParameter = sheet.ToParameter(SelectedSheetNumberParameter.Id).AsString();

                if (folderItemInfo != null)
                {
                    var sheetParameter = sheet.ToParameter(_paramId);
                    sheetItem.GroupItemName = sheetParameter.AsString().IsNullOrEmpty()
                        ? ""
                        : sheetParameter.AsString();
                }

                string sheetId = sheet.Id.ToString();
                foreach (var scheduleGraphic in scheduleGraphics)
                {
                    if (scheduleGraphic.OwnerViewId.ToString() == sheetId)
                    {
                        var scheduleItem = new ScheduleItem(this)
                        {
                            Name = "Спецификация: " + scheduleGraphic.Name,
                            Element = (scheduleGraphic as ScheduleSheetInstance).ScheduleId.ToElement<ViewSchedule>(),
                            Parent = sheetItem
                        };

                        sheetItem.ScheduleItems.Add(scheduleItem);
                    }
                }

                if (sheetItem.ScheduleItems.Count == 0) continue;

                sheetItem.ScheduleItems = sheetItem.ScheduleItems.OrderBy(s => s.Name, new NaturalComparer()).ToList();

                sections.Add(sheetItem.GroupItemName);
                _sheetItems.Add(sheetItem);
            }

            var sheetGroups = new ObservableCollection<GroupItem>();
            foreach (var section in sections)
            {
                List<SheetItem> subList = [];
                foreach (var sheetItem in _sheetItems.OrderBy(s => s.Number, new NaturalComparer()))
                {
                    if (sheetItem.GroupItemName == section) subList.Add(sheetItem);
                }

                if (subList.Count > 0)
                {
                    var groupItem = new GroupItem
                    {
                        Name = section.IsNullOrEmpty() ? "???" : section,
                        Sheets = CollectionViewSource.GetDefaultView(subList)
                    };

                    subList.ForEach(v => v.Parent = groupItem);

                    groupItem.Sheets.Filter = item =>
                    {
                        if (Filter.IsNullOrEmpty())
                            return true;

                        var sheetItem = (SheetItem)item;
                        return sheetItem.Title.Contains(Filter, StringComparison.OrdinalIgnoreCase);
                    };

                    sheetGroups.Add(groupItem);
                }
            }

            if (sheetGroups.Count == 1 && sheetGroups.First().Name == "???")
                sheetGroups.First().Name = "все";

            sheetGroups = new(sheetGroups.OrderBy(s => s.Name, new NaturalComparer()).ToList());

            SheetGroups = CollectionViewSource.GetDefaultView(sheetGroups);
            SheetGroups.Filter = item =>
            {
                var groupItem = (GroupItem)item;
                groupItem.Sheets.Refresh();

                if (Filter.IsNullOrEmpty())
                    return true;

                return groupItem.Sheets.Cast<SheetItem>().Count() != 0;
            };
        }

        [RelayCommand]
        private void SelectExcelFile()
        {
            var dialog = new OpenFileDialog();
            dialog.DefaultExt = "*.xlsx";
            dialog.Title = "Выберите файл Excel";
            dialog.Filter = "файл Excel|*.xlsx";

            if (dialog.ShowDialog() == true)
            {
                ExcelFilePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void Sort()
        {
            SpecificationItems = new(SpecificationItems
                .OrderBy(s => s.SheetNumber, new NaturalComparer())
                .ThenBy(s => s.Title, new NaturalComparer())
                .ToList());
        }

        [RelayCommand(CanExecute = nameof(IsEnabled))]
        private void Run()
        {
            if (!RefreshExistOne && CreatedSpecifications.Select(sc => sc.Name).ToList().Contains("SE Plugins_" + NewSpecificationName))
            {
                MessageBox.Show("Введите уникальное имя ведомости!", "SE Plugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RaiseCloseRequest();

            var excelSpecifications = SpecificationItems
                .Where(s => s.RevitName == "Файл Excel")
                .ToList();

            if (excelSpecifications.Count != 0)
            {
                using (Transaction t = new Transaction(RevitAPI.Document, "Генерация спецификаций из Excel"))
                {
                    t.Start();

                    foreach (var specItem in excelSpecifications)
                    {
                        var sheet = _sheets.FirstOrDefault(sh => sh.SheetNumber == specItem.SheetNumberFromExcel);
                        if (sheet != null)
                        {
                            var schedule = CreateNewExcelSpecification(specItem.Title);

                            ScheduleSheetInstance.Create(RevitAPI.Document, sheet.Id, schedule.Id, new XYZ());
                        }
                    }

                    t.Commit();
                }
            }

            ViewSchedule viewSchedule = null;

            using (Transaction t = new Transaction(RevitAPI.Document, "Ведомость спецификаций"))
            {
                t.Start();

                if (RefreshExistOne)
                    RefreshSpecification();
                else
                    viewSchedule = CreateNewSpecification();

                t.Commit();
            }

            RevitAPI.UIDocument.ActiveView = RefreshExistOne
                ? SelectedSpecification
                : viewSchedule;
        }

        private bool IsEnabled()
        {
            return RefreshExistOne
                ? SelectedSpecification != null
                : !NewSpecificationName.IsNullOrEmpty();
        }

        [RelayCommand]
        private void RemoveItem(SpecificationItem specificationItem)
        {
            specificationItem.ScheduleInstance.IsSelected = false;
        }

        private TableCellStyle GetTableCellStyle(Element boldLine, Element thickLine)
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
            if (thickLine != null)
            {
                cellStyle.BorderTopLineStyle = thickLine.Id;
                cellStyle.BorderBottomLineStyle = thickLine.Id;
            }

            cellStyle.SetCellStyleOverrideOptions(overrideOptions);

            return cellStyle;
        }

        private ViewSchedule CreateNewExcelSpecification(string name)
        {
            var schedule = ViewSchedule.CreateSchedule(RevitAPI.Document, ElementId.InvalidElementId);
            try
            {
                schedule.Name = name;
            }
            catch
            {
                MessageBox.Show($"Спецификация с именем {name} уже создана в проекте! Спецификация будет переименована.", "SE Plugins"
                    , MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var definition = schedule.Definition;
            definition.ShowHeaders = false;

            var markField = definition.AddField(ScheduleFieldType.Instance, new ElementId(BuiltInParameter.DOOR_NUMBER));
            definition.AddFilter(new ScheduleFilter(markField.FieldId, ScheduleFilterType.Equal, "SE Plugins"));

            return schedule;
        }
        private ViewSchedule CreateNewSpecification()
        {
            var schedule = ViewSchedule.CreateSchedule(RevitAPI.Document, ElementId.InvalidElementId);
            schedule.Name = "SE Plugins_" + NewSpecificationName;

            var textNoteType = schedule.TitleTextTypeId;

            var definition = schedule.Definition;
            definition.ShowHeaders = false;

            var markField = definition.AddField(ScheduleFieldType.Instance, new ElementId(BuiltInParameter.DOOR_NUMBER));
            definition.AddFilter(new ScheduleFilter(markField.FieldId, ScheduleFilterType.Equal, "SE Plugins"));

            var tableData = schedule.GetTableData();
            var bodySectionData = tableData.GetSectionData(SectionType.Body);
            var headerSectionData = tableData.GetSectionData(SectionType.Header);

            bodySectionData.SetColumnWidth(0, UnitUtils.ConvertToInternalUnits(185, ParameterMethods.GetUnitType()));

            headerSectionData.ClearCell(0, 0);

            for (var i = 0; i < 2; i++)
            {
                headerSectionData.InsertColumn(0);
            }

            headerSectionData.SetColumnWidth(0, UnitUtils.ConvertToInternalUnits(15, ParameterMethods.GetUnitType()));
            headerSectionData.SetColumnWidth(1, UnitUtils.ConvertToInternalUnits(140, ParameterMethods.GetUnitType()));
            headerSectionData.SetColumnWidth(2, UnitUtils.ConvertToInternalUnits(30, ParameterMethods.GetUnitType()));

            var temporarySheetId = _sheetItems.FirstOrDefault().Element.Id;

            foreach (var specItem in SpecificationItems
                .OrderByDescending(s => s.SheetNumber, new NaturalComparer())
                .ThenByDescending(s => s.Title, new NaturalComparer())
                .ToList())
            {
                var textNote = TextNote.Create(RevitAPI.Document, temporarySheetId, new XYZ(), specItem.Title, textNoteType);

                RevitAPI.Document.Regenerate();

                var height = textNote.Width > UnitUtils.ConvertToInternalUnits(140, ParameterMethods.GetUnitType())
                    ? 12
                    : 8;

                RevitAPI.Document.Delete(textNote.Id);

                headerSectionData.SetRowHeight(0, UnitUtils.ConvertToInternalUnits(height, ParameterMethods.GetUnitType()));

                try
                {
                    headerSectionData.SetCellText(0, 1, specItem.Title);
                    headerSectionData.SetCellText(0, 0, specItem.SheetNumber);
                }
                catch { }

                headerSectionData.InsertRow(0);
            }

            headerSectionData.SetCellText(0, 0, "Лист");
            headerSectionData.SetCellText(0, 1, "Наименование");
            headerSectionData.SetCellText(0, 2, "Примечание");
            headerSectionData.SetRowHeight(0, UnitUtils.ConvertToInternalUnits(15, ParameterMethods.GetUnitType()));

            var boldLine = new FilteredElementCollector(RevitAPI.Document)
                .WhereElementIsNotElementType()
                .FirstOrDefault(l => l.Name == "ADSK_Спецификация толстая");

            var thickLine = new FilteredElementCollector(RevitAPI.Document)
                .WhereElementIsNotElementType()
                .FirstOrDefault(l => l.Name == "ADSK_Спецификация тонкая");

            var cellStyle = GetTableCellStyle(boldLine, thickLine);
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
            var tableData = SelectedSpecification.GetTableData();
            var headerSectionData = tableData.GetSectionData(SectionType.Header);

            var textNoteType = SelectedSpecification.TitleTextTypeId;

            var rowNumbers = headerSectionData.NumberOfRows - 2;
            if (rowNumbers < SpecificationItems.Count)
            {
                var boldLine = new FilteredElementCollector(RevitAPI.Document)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault(l => l.Name == "ADSK_Cпецификация толстая");

                var thickLine = new FilteredElementCollector(RevitAPI.Document)
                    .WhereElementIsNotElementType()
                    .FirstOrDefault(l => l.Name == "ADSK_Cпецификация тонкая");

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
            else if (rowNumbers > SpecificationItems.Count)
            {
                while (rowNumbers != SpecificationItems.Count)
                {
                    headerSectionData.RemoveRow(rowNumbers);

                    rowNumbers--;
                }
            }

            var temporarySheetId = _sheetItems.FirstOrDefault().Element.Id;

            var i = 2;
            foreach (var specItem in SpecificationItems
                .OrderBy(s => s.SheetNumber, new NaturalComparer())
                .ThenBy(s => s.Title, new NaturalComparer())
                .ToList())
            {
                var textNote = TextNote.Create(RevitAPI.Document, temporarySheetId, new XYZ(), specItem.Title, textNoteType);

                RevitAPI.Document.Regenerate();

                var height = textNote.Width > UnitUtils.ConvertToInternalUnits(140, ParameterMethods.GetUnitType())
                    ? 12
                    : 8;

                RevitAPI.Document.Delete(textNote.Id);

                headerSectionData.SetRowHeight(i, UnitUtils.ConvertToInternalUnits(height, ParameterMethods.GetUnitType()));

                try
                {
                    headerSectionData.SetCellText(i, 1, specItem.Title);
                    headerSectionData.SetCellText(i, 0, specItem.SheetNumber);
                }
                catch { }

                i++;
            }
        }


        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}