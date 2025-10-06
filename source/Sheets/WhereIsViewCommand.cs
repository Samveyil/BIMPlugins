using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using BIMPlugins.ExtStorage;
using System.Windows;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.WPF;
using BIMPlugins.ExtStorage.MessageBoxes;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Sheets
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WhereIsViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var view = RevitAPI.Document.ActiveView;
            if (view.ViewType == ViewType.DrawingSheet)
            {
                MessageWindow.ShowMessage("Активным видом не должен быть лист!", MessageBoxImage.Warning);
                return Result.Succeeded;
            }

            List<ViewSheet> sheets = view.ViewType == ViewType.Schedule
                ? view
                    .GetDependentElements(new ElementClassFilter(typeof(ScheduleSheetInstance)))
                    .Select(id => id.ToElement<ScheduleSheetInstance>())
                    .Select(v => v.OwnerViewId.ToElement<ViewSheet>())
                    .ToList()
                : view
                    .GetDependentElements(new ElementClassFilter(typeof(Viewport)))
                    .Select(id => id.ToElement<Viewport>())
                    .Where(v => v.SheetId != ElementId.InvalidElementId)
                    .Select(v => v.SheetId.ToElement<ViewSheet>())
                    .ToList();

            if (sheets.Count == 0)
            {
                MessageWindow.ShowMessage($"Вид: {view.Name} не размещен ни на одном листе!", MessageBoxImage.Information);
                return Result.Succeeded;
            }

            var viewModel = new WhereIsViewViewModel(sheets);
            var window = new WhereIsViewWindow(viewModel);

            window.Show();

            return Result.Succeeded;
        }
    }
}