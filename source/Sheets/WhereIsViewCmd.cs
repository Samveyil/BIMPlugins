using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.WPF;
using BIMPlugins.Windows;
using System.Collections.Generic;
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
                MessageWindow.ShowMessage("Активным видом не должен быть лист!", MessageBoxImage.Warning);
                return Result.Succeeded;
            }

            List<ViewSheet> sheets = view.ViewType == ViewType.Schedule
                ? view
                    .GetDependentElements(new ElementClassFilter(typeof(ScheduleSheetInstance)))
                    .Select(id => id.ToElement<ScheduleSheetInstance>(doc))
                    .Select(v => v.OwnerViewId.ToElement<ViewSheet>(doc))
                    .ToList()
                : view
                    .GetDependentElements(new ElementClassFilter(typeof(Viewport)))
                    .Select(id => id.ToElement<Viewport>(doc))
                    .Where(v => v.SheetId != ElementId.InvalidElementId)
                    .Select(v => v.SheetId.ToElement<ViewSheet>(doc))
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