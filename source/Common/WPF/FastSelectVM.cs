using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BIMPlugins.Common.WPF
{
    public partial class FastSelectVM(UIDocument uiDoc, Element element) : ObservableObject
    {
        [ObservableProperty] private bool _wholeModel = true;

        private readonly UIDocument _uiDoc = uiDoc;
        private readonly Element _selectedElement = element;

        [RelayCommand]
        private void Category()
        {
            var doc = _uiDoc.Document;
            var category = _selectedElement.GetBuiltInCategory();

            _uiDoc.Selection.SetElementIds(WholeModel
                ? doc.ToElementIds(category)
                : doc.ToElementIds(doc.ActiveView.Id, category));

            RevitOptionsBar.Hide();
        }

        [RelayCommand]
        private void Family()
        {
            if (_selectedElement is not FamilyInstance familyInstance)
            {
                MessageDialog.Show("Выбранный элемент не является семейством!", "Быстрый выбор", icon: MessageIcon.Warning);
                return;
            }

            var doc = _uiDoc.Document;

            // Revit checks the family of each instance itself, without passing every instance to C#.
            var familyFilter = new ElementId(BuiltInParameter.ELEM_FAMILY_PARAM)
                .CreateEqualsFilter(familyInstance.Symbol.Family.Id);

            _uiDoc.Selection.SetElementIds(WholeModel
                ? doc.ToElementIds<FamilyInstance>(familyFilter)
                : doc.ToElementIds<FamilyInstance>(doc.ActiveView.Id, familyFilter));

            RevitOptionsBar.Hide();
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}
