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
    public class SpecificationScheduleCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var sheets = RevitAPI.Document.ToElements<ViewSheet>(BuiltInCategory.OST_Sheets);

            if (sheets.Count == 0)
            {
                MessageDialog.Show("В проекте отсутствуют листы!", icon: MessageIcon.Warning);
                return Result.Cancelled;
            }

            var window = new SpecificationScheduleWindow(new SpecificationScheduleVM(sheets));

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}