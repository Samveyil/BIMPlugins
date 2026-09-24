using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Common.WPF
{
    public partial class FastSelectViewModel(Element element) : ObservableObject
    {
        [ObservableProperty] private bool _wholeModel = true;

        private readonly Document _doc = element.Document;
        private readonly Element _selectedElement = element;

        [RelayCommand]
        private void Category()
        {
            var category = _selectedElement.GetBuiltInCategory();

            var collector = WholeModel
                ? new FilteredElementCollector(_doc)
                : new FilteredElementCollector(_doc, _doc.ActiveView.Id);

            var elementIds = collector
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .ToElementIds();

            new UIDocument(_doc).Selection.SetElementIds(elementIds);

            RevitOptionsBar.Hide();
        }

        [RelayCommand]
        private void Family()
        {
            if (_selectedElement is FamilyInstance familyInstance)
            {
                var familyName = familyInstance.Symbol.FamilyName;

                var collector = WholeModel
                    ? new FilteredElementCollector(_doc)
                    : new FilteredElementCollector(_doc, _doc.ActiveView.Id);

                var elementIds = collector
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(e => e.Symbol.FamilyName == familyName)
                    .Select(e => e.Id)
                    .ToList();

                new UIDocument(_doc).Selection.SetElementIds(elementIds);

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