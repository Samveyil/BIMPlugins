using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class SumViewModel : ObservableObject
    {
        [ObservableProperty] private double _count = 0;
        [ObservableProperty] private double _sum = 0;
        [ObservableProperty] private ObservableCollection<Parameter> _parameters = [];
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SumUpCommand))] private Parameter _selectedParameter;

        private List<Element> _elements = [];
        private readonly UIDocument _uiDoc;

        public SumViewModel()
        {
            _uiDoc = RevitAPI.UIDocument;
            
            _elements = _uiDoc.ToSelectedElements().ToList();
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
                var selectedElems = _uiDoc.ToSelectedElements().ToList();

                var elements = selectedElems.Count != 0
                    ? selectedElems
                    : _uiDoc.PickElements("Выберите элементы").ToList();
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
                    var parameter = elem.Parameters.Cast<Parameter>()
                        .FirstOrDefault(p => SelectedParameter.Definition.Name == p.Definition.Name);

                    Sum += parameter.GetValue()
                        .To<double>()
                        .ToUnit(parameter.GetUnitType());
                }

                Sum = Sum.Round(3);
            }
            catch {  }; 
        }

        private bool IsEnabled() => SelectedParameter != null;

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}