using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Parameters.WPF;
using BIMPlugins.Windows;

namespace BIMPlugins.Parameters
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SetParameterValueCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var selectedElements = RevitAPI.UIDocument.ToSelectedElements();
            if (selectedElements.Count == 0)
            {
                MessageDialog.Show("Выберите элементы!", "Заполнить параметры", icon: MessageIcon.Warning);
                return Result.Cancelled;
            }

            var window = new SetParameterValueWindow(new SetParameterValueVM(selectedElements));
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
