using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Common.WPF;
using System.Windows;
using BIMPlugins.Bars;
using System.Linq;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FastSelectCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var element = RevitAPI.UIDocument.ToSelectedElements().FirstOrDefault();
            if (element == null)
            {
                MessageBox.Show("Выберите элемент!", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                return Result.Failed;
            }

            var options = new FastSelectViewModel(element);
            var view = new FastSelectView(options);
            
            RevitOptionsBar.Show(view);

            return Result.Succeeded;
        }
    }
}