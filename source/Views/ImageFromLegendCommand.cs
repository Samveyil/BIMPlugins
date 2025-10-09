using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Views.WPF;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Views
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ImageFromLegendCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var legends = RevitAPI.UIDocument.ToSelectedElements()
                .Where(e => IsLegend(e))
                .Cast<View>()
                .ToList();

            if (legends.Count == 0)
            {
                MessageBox.Show("Укажите легенды!", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                return Result.Failed;
            }
            else
            {
                var viewModel = new ImageFromLegendViewModel(legends);
                var window = new ImageFromLegendWindow(viewModel);
                viewModel.CloseRequest += (s, e) => window.Close();

                window.ShowDialog();
            }

            return Result.Succeeded;
        }
        private bool IsLegend(Element element)
        {
            if (element.GetBuiltInCategory() == BuiltInCategory.OST_Views)
            {
                return ((View)element).ViewType == ViewType.Legend;
            }
            else { return false; }
        }
    }
}