using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Parameters.WPF;
using BIMPlugins.Windows;

namespace BIMPlugins.Parameters
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class NumerateCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;

            var element = uiDoc.PickElement("Выберите элемент, параметры которого нужно нумеровать");
            if (element == null)
                return Result.Cancelled;

            var vm = new NumerateVM(uiDoc, element);
            if (vm.Parameters.Count == 0)
            {
                MessageDialog.Show("У элемента нет текстовых параметров, в которые можно записать номер.", NumerateVM.Title, icon: MessageIcon.Warning);
                return Result.Cancelled;
            }

            var view = new NumerateView(vm);
            RevitOptionsBar.Show(view);

            return Result.Succeeded;
        }
    }
}
