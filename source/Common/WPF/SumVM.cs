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
    public partial class SumVM : ObservableObject
    {
        private const string DefaultParameterName = "Длина";

        [ObservableProperty] private int _count;
        [ObservableProperty] private double _sum;
        [ObservableProperty] private ObservableCollection<Parameter> _parameters = [];
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SumUpCommand))] private Parameter _selectedParameter;

        private readonly UIDocument _uiDoc;
        private List<Element> _elements = [];

        public SumVM()
        {
            _uiDoc = RevitAPI.UIDocument;
            SetElements(_uiDoc.ToSelectedElements().ToList());
        }

        [RelayCommand]
        private void SelectElems()
        {
            RevitOptionsBar.Hide(true);
            try
            {
                var selectedElems = _uiDoc.ToSelectedElements().ToList();

                var elements = selectedElems.Count != 0
                    ? selectedElems
                    : _uiDoc.PickElements("Выберите элементы")?.ToList();

                if (elements != null)
                    SetElements(elements);
            }
            finally
            {
                RevitOptionsBar.Show();
            }
        }

        private void SetElements(List<Element> elements)
        {
            var selectedParameterName = SelectedParameter?.Definition.Name;

            _elements = elements;
            Count = elements.Count;
            Sum = 0;

            Parameters = new(elements
                .SelectMany(elem => elem.Parameters.Cast<Parameter>()
                    .Where(p => p.StorageType == StorageType.Double || p.StorageType == StorageType.Integer))
                .GroupBy(p => p.Definition.Name)
                .Select(g => g.First())
                .OrderBy(p => p.Definition.Name));

            SelectedParameter = selectedParameterName.IsNullOrEmpty()
                ? Parameters.FirstOrDefault(p => p.Definition.Name == DefaultParameterName
                    && (p.Definition as InternalDefinition)?.BuiltInParameter != BuiltInParameter.INVALID)
                : Parameters.FirstOrDefault(p => p.Definition.Name == selectedParameterName);
        }

        [RelayCommand(CanExecute = nameof(IsEnabled))]
        private void SumUp()
        {
            var parameterName = SelectedParameter.Definition.Name;
            double sum = 0;

            foreach (var elem in _elements)
            {
                var parameter = elem.Parameters.Cast<Parameter>()
                    .FirstOrDefault(p => p.Definition.Name == parameterName && p.HasValue);

                sum += parameter?.StorageType switch
                {
                    StorageType.Double => parameter.AsDouble().ToUnit(parameter.GetUnitType()),
                    StorageType.Integer => parameter.AsInteger(),
                    _ => 0
                };
            }

            Sum = sum.Round(3);
        }

        private bool IsEnabled() => SelectedParameter != null;

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}
