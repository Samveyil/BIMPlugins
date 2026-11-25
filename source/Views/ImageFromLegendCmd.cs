using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Views.WPF;
using BIMPlugins.Windows;
using System.Linq;

namespace BIMPlugins.Views
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ImageFromLegendCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var legends = RevitAPI.UIDocument.ToSelectedElements()
                .OfType<View>()
                .Where(v => v.ViewType == ViewType.Legend)
                .ToList();

            if (legends.Count == 0)
            {
                MessageDialog.Show("Выберите легенды в диспетчере проекта!", "Изображения из легенд", icon: MessageIcon.Warning);
                return Result.Cancelled;
            }

            var window = new ImageFromLegendWindow(new ImageFromLegendVM(legends));
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
