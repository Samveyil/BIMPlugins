using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using BIMPlugins.Parameters.WPF;
using System.Linq;

namespace BIMPlugins.Parameters
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SetParameterValueCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var elems = RevitAPI.UIDocument.ToSelectedElements().ToList();
            if (elems.Count == 0)
            {
                MessageWindow.ShowMessage("Выберите элементы!", icon: System.Windows.MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var vm = new SetParameterValueVM(elems);
            var window = new SetParameterValueWindow(vm);
            vm.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}