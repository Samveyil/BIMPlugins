using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.Windows;
using BIMPlugins.Sheets.WPF;
using System.Linq;

namespace BIMPlugins.Sheets
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CopyStampCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var sheets = new FilteredElementCollector(RevitAPI.Document)
                .OfCategory(BuiltInCategory.OST_Sheets)
                .WhereElementIsNotElementType()
                .Cast<ViewSheet>()
                .ToList();

            if (sheets.Count == 0)
            {
                MessageWindow.ShowMessage("В проекте отсутствуют листы!", System.Windows.MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var viewModel = new CopyStampViewModel(sheets);
            var window = new CopyStampWindow(viewModel);
            viewModel.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}