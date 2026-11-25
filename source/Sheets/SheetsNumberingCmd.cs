using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.WPF;
using BIMPlugins.Windows;

namespace BIMPlugins.Sheets
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SheetsNumberingCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var sheets = RevitAPI.Document.ToElements<ViewSheet>(BuiltInCategory.OST_Sheets);

            if (sheets.Count == 0)
            {
                MessageWindow.ShowMessage("В проекте отсутствуют листы!", icon: System.Windows.MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var vm = new SheetsNumberingVM(sheets);
            var window = new SheetsNumberingWindow(vm);
            vm.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}