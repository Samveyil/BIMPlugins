using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Families.Classes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace BIMPlugins.Families.WPF
{
    public partial class FamilyCheckerVM : ObservableObject
    {
        [ObservableProperty] private static ObservableCollection<ParameterItem> _parameters;
        [ObservableProperty] private static ObservableCollection<ParameterItem> _selectedItems = [];

        [ObservableProperty] private int _testsCount = 15;
        [ObservableProperty] private int _testIndex = 1;
        [ObservableProperty] private bool _isGenerated = false;

        private readonly Document _doc;
        private readonly FamilyInstance _famInst;
        private readonly XYZ _startPoint;
        private readonly XYZ _dir;

        private ExternalEvent ExEvent { get; }
        private List<ParametersTestSet> ParametersTests { get; set; } = [];

        public FamilyCheckerVM(FamilyInstance famInst)
        {
            _doc = RevitAPI.Document;
            _famInst = famInst;

            if (_famInst.Location is LocationCurve locationCurve)
            {
                var curve = locationCurve.Curve as Line;
                _startPoint = curve.GetEndPoint(0);
                _dir = curve.Direction.Normalize();
            }

            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.SetParameters());

            var famDoc = _doc.EditFamily(_famInst.Symbol.Family);
            var famManager = famDoc.FamilyManager;
            var tableManager = FamilySizeTableManager.GetFamilySizeTableManager(famDoc, new ElementId(BuiltInParameter.RBS_LOOKUP_TABLE_NAME));

            var famParameters = famManager.GetParameters();

            Parameters = new(famParameters
                .Where(p => (p.StorageType == StorageType.Integer || p.StorageType == StorageType.Double) && !p.IsDeterminedByFormula && p.Formula.IsNullOrEmpty())
                .Select(p => new ParameterItem(p))
                .OrderBy(p => p.Group)
                .ToList()
            );

            var orderedParameters = famParameters
                .OrderByDescending(p => p.Definition.Name.Length)
                .ToList();

            foreach (var parameter in famParameters.Where(p => !p.Formula.IsNullOrEmpty() && p.Formula.Contains("size_lookup")))
            {
                var formula = GetSizeLookupFormula(parameter.Formula);
                var splitFormuls = formula.Split(',');

                foreach (var famParam in GetUsedParameters(formula, orderedParameters))
                {
                    var tableParam = famManager.get_Parameter(splitFormuls[0].Trim());

                    ParameterItem paramItem;
                    if (!Parameters.Select(p => p.Name).Contains(famParam.Definition.Name))
                    {
                        paramItem = new ParameterItem(famParam);
                        Parameters.Add(paramItem);
                    }
                    else
                        paramItem = Parameters.FirstOrDefault(p => p.Name == famParam.Definition.Name);

                    paramItem.ModeIndex = 3;
                    paramItem.FamilySizeTable = tableManager.GetSizeTable(famManager.CurrentType.AsString(tableParam));

                    int index = Array.FindIndex(
                        splitFormuls,
                        x => x.Trim() == famParam.Definition.Name);

                    paramItem.ColumnNumber = index >= 3
                        ? index - 2
                        : 0;
                }
            }

            famDoc.Close(false);
        }

        [RelayCommand]
        private void RemoveParameter(ParameterItem parameterItem)
        {
            if (SelectedItems.Count > 1)
                foreach (ParameterItem item in SelectedItems.ToList())
                    Parameters.Remove(item);

            if (Parameters.Contains(parameterItem))
                Parameters.Remove(parameterItem);
        }

        [RelayCommand]
        private void RemoveParameterGroup(string parameterGroup)
        {
            foreach (var paramterItem in Parameters.Where(p => p.Group == parameterGroup).ToList())
                Parameters.Remove(paramterItem);
        }

        [RelayCommand]
        private void CheckParameter(ParameterItem parameterItem)
        {
            var check = parameterItem.IsSelected;

            if (SelectedItems.Count > 1)
                foreach (ParameterItem item in SelectedItems)
                    item.IsSelected = check;
        }

        [RelayCommand]
        private void Update() => ExEvent.Raise();

        private void SetParameters()
        {
            using (Transaction t = new Transaction(_doc, "Задать параметры"))
            {
                t.Start();

                var testSet = ParametersTests[TestIndex - 1];

                foreach (var kvp in testSet.ParameterValues)
                {
                    var param = _famInst.LookupParameter(kvp.Key) ?? _famInst.Symbol.LookupParameter(kvp.Key);
                    var value = param.StorageType == StorageType.Double
                        ? Convert.ToDouble(kvp.Value, CultureInfo.InvariantCulture).FromUnit(param.GetUnitType())
                        : kvp.Value;

                    if (kvp.Key == "Длина" && _famInst.Location is LocationCurve locationCurve)
                        locationCurve.Curve = Line.CreateBound(_startPoint, _startPoint + _dir * (double)value);
                    else
                        param.SetValue(value);
                }

                t.Commit();
            }
        }

        [RelayCommand]
        private void GenerateTests()
        {
            ParametersTests.Clear();

            var parameterItems = Parameters
                .Where(p => p.IsSelected)
                .ToList();

            for (int i = 0; i < TestsCount; i++)
            {
                var testSet = new ParametersTestSet(i);

                foreach (var paramItem in parameterItems.Where(p => p.ModeIndex == 0))
                {
                    var value = paramItem.IsYesNo
                        ? RandomValueGenerator.GenerateRandomBool()
                        : RandomValueGenerator.GenerateRandomInt(paramItem.MinValue, paramItem.MaxValue, paramItem.Step);

                    testSet.ParameterValues[paramItem.Name] = value;
                }

                foreach (var paramItem in parameterItems.Where(p => p.ModeIndex == 1))
                {
                    testSet.ParameterValues[paramItem.Name] = paramItem.FixedValue;
                }

                foreach (var paramItem in parameterItems.Where(p => p.ModeIndex == 2))
                {
                    testSet.ParameterValues[paramItem.Name] = RandomValueGenerator.GetRandomValue(paramItem.Values);
                }
                
                foreach (var paramItem in parameterItems.Where(p => p.ModeIndex == 3))
                {
                    var table = paramItem.FamilySizeTable;
                    if (table == null)
                        continue;

                    var rowNumber = RandomValueGenerator.GenerateRandomInt(1, table.NumberOfRows - 1, 1);

                    var value = table.AsValueString(rowNumber, paramItem.ColumnNumber);
                    if (value.IsNullOrEmpty())
                        value = table.AsValueString(rowNumber + 1, paramItem.ColumnNumber);

                    testSet.ParameterValues[paramItem.Name] = value;
                }

                ParametersTests.Add(testSet);
            }

            IsGenerated = true;

            if (TestIndex != 1)
                TestIndex = 1;
            else
                ExEvent.Raise();
        }

        private List<FamilyParameter> GetUsedParameters(string formula, List<FamilyParameter> parameters)
        {
            var usedParameters = new List<FamilyParameter>();
            string remainingFormula = formula;

            foreach (FamilyParameter parameter in parameters)
            {
                string name = parameter.Definition.Name;

                string pattern =
                    $@"(?<![\p{{L}}\p{{N}}_])" +
                    $@"{Regex.Escape(name)}" +
                    $@"(?![\p{{L}}\p{{N}}_])";

                if (!Regex.IsMatch(remainingFormula, pattern, RegexOptions.CultureInvariant))
                    continue;

                usedParameters.Add(parameter);

                remainingFormula = Regex.Replace(remainingFormula, pattern, " ", RegexOptions.CultureInvariant);
            }

            return usedParameters
                .Where(p => p.Formula.IsNullOrEmpty())
                .ToList();
        }

        private string GetSizeLookupFormula(string formula)
        {
            string functionName = "size_lookup";

            int functionIndex = formula.IndexOf(
                functionName,
                StringComparison.OrdinalIgnoreCase);

            int openParenthesisIndex =
                formula.IndexOf('(', functionIndex + functionName.Length);

            int closeParenthesisIndex = formula.IndexOf(')', openParenthesisIndex);

            string argumentsText = formula.Substring(
                openParenthesisIndex + 1,
                closeParenthesisIndex - openParenthesisIndex - 1);

            return argumentsText;
        }
    }
}