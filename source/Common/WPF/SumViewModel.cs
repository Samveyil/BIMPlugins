using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Bars;
using System.Collections.ObjectModel;
using BIMPlugins.ExtStorage.Methods;
using System.Collections.Generic;
using System.Linq;
using System;


namespace BIMPlugins.Common.WPF
{
    public partial class SumViewModel : ObservableObject
    {
        [ObservableProperty] private double _count = 0;
        [ObservableProperty] private double _sum = 0;
        [ObservableProperty] private ObservableCollection<Parameter> _parameters = [];
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SumUpCommand))] private Parameter _selectedParameter;

        private List<Element> _elements = [];

        public SumViewModel()
        {
            _elements = RevitAPI.UIDocument.ToSelectedElements();
            Count = _elements.Count;

            if (Count != 0)
            {
                Parameters = new(_elements
                    .SelectMany(elem => elem.Parameters.Cast<Parameter>()
                        .Where(p => p.StorageType == StorageType.Double || p.StorageType == StorageType.Integer))
                    .GroupBy(p => p.Definition.Name)
                    .Select(g => g.First())
                    .OrderBy(p => p.Definition.Name)
                    .ToList()
                );

                SelectedParameter = Parameters
                    .FirstOrDefault(p => p.Definition.Name == "Длина" && (p.Definition as InternalDefinition).BuiltInParameter != BuiltInParameter.INVALID);
            }
        }

        [RelayCommand]
        private void SelectElems()
        {
            Sum = 0;
            var selectedParameterName = SelectedParameter?.Definition.Name;

            RevitOptionsBar.Hide(true);
            try
            {
                var selectedElems = RevitAPI.UIDocument.ToSelectedElements();

                var elements = selectedElems.Count != 0
                    ? selectedElems
                    : SelectionMethods.PickObjects("Выберите элементы");
                Count = elements.Count;

                _elements = elements;
                Parameters = new(_elements
                    .SelectMany(elem => elem.Parameters.Cast<Parameter>()
                        .Where(p => p.StorageType == StorageType.Double || p.StorageType == StorageType.Integer))
                    .GroupBy(p => p.Definition.Name)
                    .Select(g => g.First())
                    .OrderBy(p => p.Definition.Name)
                    .ToList()
                );

                SelectedParameter = selectedParameterName.IsNullOrEmpty()
                    ? Parameters.FirstOrDefault(p => p.Definition.Name == "Длина" && (p.Definition as InternalDefinition).BuiltInParameter != BuiltInParameter.INVALID)
                    : Parameters.FirstOrDefault(p => p.Definition.Name == selectedParameterName);
            }
            catch { }
            finally
            {
                RevitOptionsBar.Show();
            }
        }

        [RelayCommand(CanExecute = nameof(IsEnabled))]
        private void SumUp()
        {
            Sum = 0;
            try
            {
                foreach (var elem in _elements)
                {
                    var parameter = elem.Parameters.Cast<Parameter>().FirstOrDefault(p => SelectedParameter.Definition.Name == p.Definition.Name);

                    Sum += UnitUtils.ConvertFromInternalUnits((double)parameter.GetValue(), parameter.GetUnitType());
                }

                Sum = Math.Round(Sum, 3);
            }
            catch {  }; 
        }

        private bool IsEnabled() => SelectedParameter != null;

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}