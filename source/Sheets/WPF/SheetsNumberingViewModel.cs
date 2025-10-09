using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.Classes;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Sheets.WPF
{
    public partial class SheetsNumberingViewModel : ObservableObject
    {
        [ObservableProperty] private bool _userSettings = true;
        [ObservableProperty] private int _startNumber = 0;
        [ObservableProperty] private string _prefix;
        [ObservableProperty] private string _suffix;
        [ObservableProperty] private int _numberLength = 2;
        [ObservableProperty] private string _selectedWayToChangeNumber = "Увеличить на";
        [ObservableProperty] private int _step = 0;
        [ObservableProperty] private bool _writeNumberToParameter = true;
        [ObservableProperty] private Parameter _selectedParameter;
        [ObservableProperty] private ObservableCollection<GroupItem> _sheetSections = [];
        [ObservableProperty] private GroupItem _selectedGroup;

        partial void OnNumberLengthChanged(int value)
        {
            ChangeNumber();
        }
        partial void OnPrefixChanged(string value)
        {
            ChangeNumber();
        }
        partial void OnSuffixChanged(string value)
        {
            ChangeNumber();
        }
        partial void OnStepChanged(int value)
        {
            ChangeNumber(SelectedWayToChangeNumber);
        }
        partial void OnStartNumberChanged(int value)
        {
            ChangeNumber();
        }
        partial void OnSelectedWayToChangeNumberChanged(string value)
        {
            ChangeNumber(value);
        }
        partial void OnUserSettingsChanged(bool value)
        {
            StartNumber = 0;
            NumberLength = 2;
            Step = 0;

            ReturnNumbers();
        }
        partial void OnSelectedGroupChanged(GroupItem value)
        {
            StartNumber = 0;
            NumberLength = 2;
            Step = 0;

            ReturnNumbers();
        }
        partial void OnSelectedParameterChanged(Parameter value)
        {
            if (SelectedGroup == null)
                return;
            
            foreach (var sheetItem in SelectedGroup.Sheets.Cast<SheetNumberingItem>().ToList())
            {
                sheetItem.NumberToParameter = sheetItem.Element.ToParameter(value.Id).AsString();
            }

            ChangeNumber();
        }

        public const string NumberLengthName = nameof(NumberLength);
        public const string StartNumberName = nameof(StartNumber);
        public const string StepName = nameof(Step);

        public List<string> WaysToChangeNumber { get; set; } = ["Увеличить на", "Уменьшить на"];
        public List<Parameter> Parameters { get; set; }

        public SheetsNumberingViewModel(List<ViewSheet> sheets)
        {
            Parameters = new(
                sheets[0].Parameters.Cast<Parameter>()
                    .Where(parameter => !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
                    .OrderBy(parameter => parameter.Definition.Name)
            );

            SelectedParameter = Parameters.FirstOrDefault(p => p.IsShared && p.GUID == new Guid("b4e34c05-d510-468f-bd86-e753486c8add")) ?? Parameters.First();

            var bo = BrowserOrganization.GetCurrentBrowserOrganizationForSheets(RevitAPI.Document);

            FolderItemInfo folderItemInfo;
            ElementId paramId;
            
            try
            {
                folderItemInfo = bo.GetFolderItems(sheets[0].Id)[0];

                paramId = folderItemInfo.ElementId;
            }
            catch { folderItemInfo = null; paramId = ElementId.InvalidElementId; }

            var sheetListItems = new List<SheetNumberingItem>();
            var sections = new HashSet<string>();

            foreach (var sheet in sheets)
            {
                var sheetItem = new SheetNumberingItem(sheet);
                sheetItem.NumberToParameter = sheet.ToParameter(SelectedParameter.Id).AsString();

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

            foreach (var section in sections)
            {
                List<SheetNumberingItem> subList = [];
                foreach (var sheetListItem in sheetListItems.OrderBy(s => s.Number, new NaturalComparer()))
                {
                    if (sheetListItem.GroupItemName == section) subList.Add(sheetListItem);
                }

                if (subList.Count > 0)
                {
                    var groupSheets = new GroupItem
                    {
                        Name = section.IsNullOrEmpty()
                            ? "???"
                            : section,
                        Sheets = CollectionViewSource.GetDefaultView(subList)
                    };

                    subList.ForEach(v => v.Parent = groupSheets);

                    SheetSections.Add(groupSheets);
                }
            }

            if (SheetSections.Count == 1 && SheetSections.First().Name == "???")
                SheetSections.First().Name = "все";

            SheetSections = new(SheetSections.OrderBy(s => s.Name, new NaturalComparer()).ToList());
        }

        [RelayCommand]
        private void TreeItemClicked(object item)
        {
            if (item is GroupItem groupSheets)
            {
                SelectedGroup = groupSheets;  
            }
            else if (item is SheetNumberingItem sheetItem)
            {
                SelectedGroup = sheetItem.Parent;
            }

            SelectedGroup.IsSelected = true;
        }

        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();

            using (Transaction t = new Transaction(RevitAPI.Document, "Нумератор листов"))
            {
                t.Start();

                var group = SheetSections.FirstOrDefault(s => s.Name == SelectedGroup.Name);

                int i = 0;
                foreach (var sheetElement in group.Sheets.Cast<SheetNumberingItem>().ToList())
                {
                    var sheet = sheetElement.Element;

                    var parameter = sheet.get_Parameter(BuiltInParameter.SHEET_NUMBER);
                    parameter.Set($"1000{i}");

                    i++;
                }

                using (RevitProgressBar revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Нумерация листов...", group.Sheets.Cast<SheetNumberingItem>().ToList(), (sheetElement) =>
                    {
                        var sheet = sheetElement.Element;

                        sheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).Set(sheetElement.NewNumber);
                        
                        if (WriteNumberToParameter)
                        {
                            sheet.ToParameter(SelectedParameter.Id).Set(sheetElement.NumberToParameter);
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

            MessageBox.Show("Листы успешно пронумерованы!", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Plus(string tag)
        {
            switch (tag)
            {
                case StartNumberName:
                    StartNumber++;
                    break;
                
                case NumberLengthName:
                    NumberLength++;
                    break;

                case StepName:
                    Step++;
                    break;
            }
        }
        
        [RelayCommand]
        private void Minus(string tag)
        {
            switch (tag)
            {
                case StartNumberName:
                    StartNumber--;
                    if (StartNumber < 0)
                        StartNumber = 0;
                    break;

                case NumberLengthName:
                    NumberLength--;
                    if (NumberLength < 0)
                        NumberLength = 0;
                    break;

                case StepName:
                    Step--;
                    if (Step < 0)
                        Step = 0;
                    break;
            }
        }

        private void ChangeNumber(string way="Увеличить на")
        {
            var group = SheetSections.FirstOrDefault(s => s.Name == SelectedGroup.Name);
            if (UserSettings)
            {
                var i = StartNumber;
                foreach (var sheetElement in group.Sheets.Cast<SheetNumberingItem>().ToList())
                {
                    string stringNumber = i.ToString();
                    sheetElement.NumberToParameter = stringNumber;

                    int charCount = stringNumber.Length;
                    if (charCount < NumberLength)
                    {
                        stringNumber = string.Concat(Enumerable.Repeat("0", NumberLength - charCount)) + stringNumber;
                    }

                    sheetElement.NewNumber = Prefix + stringNumber + Suffix;
                    sheetElement.WarningImageVisibility = IsNumberExist(sheetElement)
                        ? System.Windows.Visibility.Visible
                        : System.Windows.Visibility.Collapsed;

                    i++;
                }
            }
            else
            {
                foreach (var sheetElement in group.Sheets.Cast<SheetNumberingItem>().ToList())
                {
                    var sheetNumber = sheetElement.Number;

                    Match match = Regex.Match(sheetNumber, @"(\d+)[^\d]*$");
                    if (match.Success)
                    {
                        var number = int.Parse(match.Groups[1].Value);
                        
                        if (way == "Увеличить на")
                            number += Step;
                        else
                            number -= Step;

                        string stringNumber = number.ToString();
                        var numberLength = match.Length;

                        int charCount = stringNumber.Length;
                        if (charCount < numberLength)
                            stringNumber = string.Concat(Enumerable.Repeat("0", numberLength - charCount)) + stringNumber;

                        sheetElement.NewNumber = sheetNumber.Substring(0, match.Index) + stringNumber;
                        sheetElement.NumberToParameter = number.ToString();
                        sheetElement.WarningImageVisibility = IsNumberExist(sheetElement)
                            ? System.Windows.Visibility.Visible
                            : System.Windows.Visibility.Collapsed;
                    }
                }
            }
        }
        private void ReturnNumbers()
        {
            if (SelectedGroup == null)
                return;

            foreach (var sheetItem in SelectedGroup.Sheets.Cast<SheetNumberingItem>().ToList())
            {
                sheetItem.NewNumber = sheetItem.Number;
            }
        }
        private bool IsNumberExist(SheetNumberingItem sheet)
        {
            foreach (var section in SheetSections)
            {
                foreach (var sheetElement in section.Sheets.Cast<SheetNumberingItem>())
                {
                    if (sheetElement.Number == sheet.NewNumber && sheetElement.GroupItemName != sheet.GroupItemName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}