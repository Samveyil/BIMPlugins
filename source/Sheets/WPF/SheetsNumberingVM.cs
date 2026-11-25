using Autodesk.Revit.DB;
using BIMPlugins.Bars;
using BIMPlugins.Classes;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Visibility = System.Windows.Visibility;

namespace BIMPlugins.Sheets.WPF
{
    public enum NumberChange { Increase, Decrease }

    public partial class SheetsNumberingVM : ObservableObject
    {
        private const string Title = "Нумератор листов";

        // The shared parameter "Номер листа" of ADSK, chosen by default to get the number written to.
        private static readonly Guid SheetNumberParameterGuid = new("b4e34c05-d510-468f-bd86-e753486c8add");

        // The last number of a sheet number and what follows it: "A-12a" is "12" and "a".
        private static readonly Regex LastNumber = new(@"(\d+)(\D*)$");

        [ObservableProperty] private bool _userSettings = true;
        [ObservableProperty] private int _startNumber = 0;
        [ObservableProperty] private string _prefix;
        [ObservableProperty] private string _suffix;
        [ObservableProperty] private int _numberLength = 2;
        [ObservableProperty] private NumberChange _selectedWayToChangeNumber = NumberChange.Increase;
        [ObservableProperty] private int _step = 0;
        [ObservableProperty] private bool _addInvisibleChars;
        [ObservableProperty] private bool _writeNumberToParameter = true;
        [ObservableProperty] private Parameter _selectedParameter;
        [ObservableProperty] private List<GroupItem> _sheetSections;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private GroupItem _selectedGroup;

        partial void OnNumberLengthChanged(int value) => ChangeNumber();
        partial void OnPrefixChanged(string value) => ChangeNumber();
        partial void OnSuffixChanged(string value) => ChangeNumber();
        partial void OnStepChanged(int value) => ChangeNumber();
        partial void OnStartNumberChanged(int value) => ChangeNumber();
        partial void OnSelectedWayToChangeNumberChanged(NumberChange value) => ChangeNumber();
        partial void OnAddInvisibleCharsChanged(bool value) => ChangeNumber();

        partial void OnUserSettingsChanged(bool value) => ResetSettings();
        partial void OnSelectedGroupChanged(GroupItem value) => ResetSettings();

        partial void OnSelectedParameterChanged(Parameter value)
        {
            if (SelectedGroup == null || value == null)
                return;

            foreach (var sheetItem in SelectedGroup.Sheets.Cast<SheetNumberingItem>())
                sheetItem.NumberToParameter = sheetItem.Element.ToParameter(value.Id)?.AsString();

            ChangeNumber();
        }

        private readonly Document _doc;

        public const string NumberLengthName = nameof(NumberLength);
        public const string StartNumberName = nameof(StartNumber);
        public const string StepName = nameof(Step);

        public IReadOnlyList<Option<NumberChange>> WaysToChangeNumber { get; } =
        [
            new(NumberChange.Increase, "Увеличить на"),
            new(NumberChange.Decrease, "Уменьшить на")
        ];

        public List<Parameter> Parameters { get; }

        public SheetsNumberingVM(IList<ViewSheet> sheets)
        {
            _doc = RevitAPI.Document;

            Parameters = sheets[0].Parameters
                .Cast<Parameter>()
                .Where(p => !p.IsReadOnly && p.StorageType == StorageType.String)
                .OrderBy(p => p.Definition.Name)
                .ToList();

            SelectedParameter = Parameters.FirstOrDefault(p => p.IsShared && p.GUID == SheetNumberParameterGuid) ?? Parameters.FirstOrDefault();

            var sheetItems = sheets.Select(sheet => new SheetNumberingItem(sheet)
            {
                NumberToParameter = SelectedParameter == null ? null : sheet.ToParameter(SelectedParameter.Id)?.AsString()
            }).ToList();

            SheetSections = Classes.SheetGroups.Create(_doc, sheetItems);
        }

        [RelayCommand]
        private void TreeItemClicked(object item)
        {
            var group = item switch
            {
                GroupItem groupItem => groupItem,
                SheetNumberingItem sheetItem => sheetItem.Parent,
                _ => null
            };

            if (group == null)
                return;

            SelectedGroup = group;
            SelectedGroup.IsExpanded = true;
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            var sheetItems = SelectedGroup.Sheets.Cast<SheetNumberingItem>().ToList();

            // A number taken twice would fail the whole renumbering half-way.
            var takenNumbers = sheetItems.Where(s => s.WarningImageVisibility == Visibility.Visible).Select(s => s.NewNumber).ToList();
            if (takenNumbers.Count != 0)
            {
                MessageWindow.ShowMessage(
                    $"Номера повторяются или уже заняты листами других групп: {string.Join(", ", takenNumbers)}.\n" +
                    "Включите «Добавлять невидимые символы к повторяющимся номерам», чтобы оставить эти номера.",
                    Title, icon: MessageBoxImage.Warning);
                return;
            }

            RaiseCloseRequest();

            using (Transaction t = new Transaction(_doc, Title))
            {
                t.Start();

                // The sheets first get numbers nobody has, so that two sheets of the group may swap their numbers.
                foreach (var sheetItem in sheetItems)
                    sheetItem.Element.get_Parameter(BuiltInParameter.SHEET_NUMBER).Set($"BIMPlugins_{Guid.NewGuid():N}");

                using (RevitProgressBar revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Нумерация листов...", sheetItems, sheetItem =>
                    {
                        var sheet = sheetItem.Element;
                        sheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).Set(sheetItem.NewNumber);

                        if (WriteNumberToParameter && SelectedParameter != null)
                            sheet.ToParameter(SelectedParameter.Id)?.Set(sheetItem.NumberToParameter);
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                t.Commit();
            }

            MessageWindow.ShowMessage("Листы успешно пронумерованы!", Title, icon: MessageBoxImage.Information);
        }

        private bool CanRun() => SelectedGroup != null;

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
                    StartNumber = Math.Max(0, StartNumber - 1);
                    break;

                case NumberLengthName:
                    NumberLength = Math.Max(0, NumberLength - 1);
                    break;

                case StepName:
                    Step = Math.Max(0, Step - 1);
                    break;
            }
        }

        private void ResetSettings()
        {
            StartNumber = 0;
            NumberLength = 2;
            Step = 0;

            ReturnNumbers();
        }

        private void ChangeNumber()
        {
            if (SelectedGroup == null)
                return;

            var sheetItems = SelectedGroup.Sheets.Cast<SheetNumberingItem>().ToList();

            // Numbers of the sheets out of this group and the new numbers given so far: a new number must be none of them.
            // The old numbers of this group are freed by the renumbering.
            var takenNumbers = SheetSections
                .SelectMany(g => g.Sheets)
                .Except(sheetItems)
                .Select(s => s.Number)
                .ToHashSet();

            var number = StartNumber;
            foreach (var sheetItem in sheetItems)
            {
                string newNumber;
                if (UserSettings)
                {
                    sheetItem.NumberToParameter = number.ToString();
                    newNumber = Prefix + number.ToString().PadLeft(NumberLength, '0') + Suffix;
                    number++;
                }
                else
                {
                    // The invisible characters a number already has are added anew, only as many as it needs now.
                    var sourceNumber = AddInvisibleChars ? InvisibleChars.Remove(sheetItem.Number) : sheetItem.Number;

                    // The last number of the sheet number is moved by the step; what comes before and after it stays.
                    var match = LastNumber.Match(sourceNumber);
                    if (match.Success)
                    {
                        var digits = match.Groups[1];
                        var changed = long.Parse(digits.Value) + (SelectedWayToChangeNumber == NumberChange.Increase ? Step : -Step);
                        changed = Math.Max(0, changed);

                        newNumber = sourceNumber.Substring(0, digits.Index) + changed.ToString().PadLeft(digits.Length, '0') + match.Groups[2].Value;
                        sheetItem.NumberToParameter = changed.ToString();
                    }
                    else
                    {
                        newNumber = sourceNumber;
                    }
                }

                // A taken number gets invisible characters until it is free: "1", then "1" + one character and so on.
                if (AddInvisibleChars)
                {
                    while (takenNumbers.Contains(newNumber))
                        newNumber += InvisibleChars.Symbol;
                }

                sheetItem.NewNumber = newNumber;
                sheetItem.WarningImageVisibility = takenNumbers.Contains(newNumber)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                takenNumbers.Add(newNumber);
            }
        }

        private void ReturnNumbers()
        {
            if (SelectedGroup == null)
                return;

            foreach (var sheetItem in SelectedGroup.Sheets.Cast<SheetNumberingItem>())
            {
                sheetItem.NewNumber = sheetItem.Number;
                sheetItem.WarningImageVisibility = Visibility.Collapsed;
            }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
