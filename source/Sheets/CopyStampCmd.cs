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
    public class CopyStampCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var sheets = RevitAPI.Document.ToElements<ViewSheet>(BuiltInCategory.OST_Sheets);

            if (sheets.Count == 0)
            {
                MessageWindow.ShowMessage("В проекте отсутствуют листы!", icon: System.Windows.MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var window = new CopyStampWindow(new CopyStampVM(sheets));

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}