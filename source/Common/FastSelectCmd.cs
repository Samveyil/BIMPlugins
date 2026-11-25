using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.Common.WPF;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FastSelectCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;

            var element = uiDoc.ToSelectedElements()
                .FirstOrDefault();
            if (element == null)
            {
                MessageWindow.ShowMessage("Выберите элемент!", "Быстрый выбор", icon: MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var view = new FastSelectView(new FastSelectVM(uiDoc, element));

            RevitOptionsBar.Show(view);

            return Result.Succeeded;
        }
    }
}
