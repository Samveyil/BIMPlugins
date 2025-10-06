using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.MessageBoxes;
using BIMPlugins.Parameters.WPF;

namespace BIMPlugins.Parameters
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SetParameterValueCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var elems = RevitAPI.UIDocument.ToSelectedElements();
            if (elems.Count == 0)
            {
                MessageWindow.ShowMessage("Выберите элементы!", System.Windows.MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var viewModel = new SetParameterValueViewModel(elems);
            var window = new SetParameterValueWindow(viewModel);
            viewModel.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}