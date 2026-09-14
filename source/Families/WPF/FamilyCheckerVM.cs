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
using System.Linq;

namespace BIMPlugins.Families.WPF
{
    public partial class FamilyCheckerVM : ObservableObject
    {
        [ObservableProperty] private static ObservableCollection<ParameterItem> _parameters;
        [ObservableProperty] private static ObservableCollection<ParameterItem> _selectedItems = [];

        [ObservableProperty] private int _testsCount = 50;
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
            
            Parameters = new (famManager.GetParameters()
                .Where(p => (p.StorageType == StorageType.Integer || p.StorageType == StorageType.Double) && !p.IsDeterminedByFormula && p.Formula.IsNullOrEmpty())
                .Select(p => new ParameterItem(p))
                .OrderBy(p => p.Group)
                .ToList()
            );

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
                        ? Convert.ToDouble(kvp.Value).FromUnit(param.GetUnitType())
                        : kvp.Value;

                    if (kvp.Key == "Длина" && _famInst.Location is LocationCurve locationCurve)
                    {
                        locationCurve.Curve = Line.CreateBound(_startPoint, _startPoint + _dir * (double)value);
                    }
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

                foreach (var paramItem in parameterItems.Where(p => !p.IsFixed))
                {
                    var value = paramItem.IsYesNo
                        ? RandomValueGenerator.GenerateRandomBool()
                        : RandomValueGenerator.GenerateRandomInt(paramItem.MinValue, paramItem.MaxValue, paramItem.Step);

                    testSet.ParameterValues[paramItem.Name] = value;
                }

                foreach (var paramItem in parameterItems.Where(p => p.IsFixed))
                {
                    testSet.ParameterValues[paramItem.Name] = paramItem.FixedValue;
                }

                ParametersTests.Add(testSet);
            }

            IsGenerated = true;

            if (TestIndex != 1)
                TestIndex = 1;
            else
                ExEvent.Raise();
        }
    }
}