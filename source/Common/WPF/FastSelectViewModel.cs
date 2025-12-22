using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Bars;
using System.Windows;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class FastSelectViewModel : ObservableObject
    {
        [ObservableProperty] private bool _wholeModel = true;

        private Element _selectedElement {  get; set; }

        public FastSelectViewModel(Element element)
        {
            _selectedElement = element;
        }


        [RelayCommand]
        private void Category()
        {
            var category = _selectedElement.GetBuiltInCategory();

            var collector = WholeModel
                ? new FilteredElementCollector(RevitAPI.Document)
                : new FilteredElementCollector(RevitAPI.Document, RevitAPI.Document.ActiveView.Id);

            var elementIds = collector
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElementIds();

            RevitAPI.UIDocument.Selection.SetElementIds(elementIds);

            RevitOptionsBar.Hide();
        }

        [RelayCommand]
        private void Family()
        {
            if (_selectedElement is FamilyInstance familyInstance)
            {
                var familyName = familyInstance.Symbol.FamilyName;

                var collector = WholeModel
                    ? new FilteredElementCollector(RevitAPI.Document)
                    : new FilteredElementCollector(RevitAPI.Document, RevitAPI.Document.ActiveView.Id);

                var elementIds = collector
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(e => e.Symbol.FamilyName == familyName)
                    .Select(e => e.Id)
                    .ToList();

                RevitAPI.UIDocument.Selection.SetElementIds(elementIds);

                RevitOptionsBar.Hide();
            }
            else
            {
                MessageBox.Show("Выбранный элемент не является семейством!", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}