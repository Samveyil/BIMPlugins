using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.WPF;
using BIMPlugins.Windows;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Sheets
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WhereIsViewCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;
            var view = doc.ActiveView;
            if (view.ViewType == ViewType.DrawingSheet)
            {
                MessageWindow.ShowMessage("Активным видом не должен быть лист!", icon: MessageBoxImage.Warning);
                return Result.Succeeded;
            }

            // A schedule or a legend may stand on one sheet more than once: each sheet is listed once.
            var sheetIds = view.ViewType == ViewType.Schedule
                ? view
                    .GetDependentElements(new ElementClassFilter(typeof(ScheduleSheetInstance)))
                    .Select(id => id.ToElement<ScheduleSheetInstance>(doc).OwnerViewId)
                : view
                    .GetDependentElements(new ElementClassFilter(typeof(Viewport)))
                    .Select(id => id.ToElement<Viewport>(doc).SheetId)
                    .Where(id => id != ElementId.InvalidElementId);

            var sheets = sheetIds
                .Distinct()
                .Select(id => id.ToElement<ViewSheet>(doc))
                .ToList();

            if (sheets.Count == 0)
            {
                MessageWindow.ShowMessage($"Вид: {view.Name} не размещен ни на одном листе!", icon: MessageBoxImage.Information);
                return Result.Succeeded;
            }

            var window = new WhereIsViewWindow(new WhereIsViewVM(sheets));
            window.Show();

            return Result.Succeeded;
        }
    }
}