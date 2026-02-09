using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Test2dRebar.WPF;
using System;
using System.Linq;

namespace BIMPlugins.Test2dRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PalkaViewManagerCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var palka = RevitAPI.UIDocument.ToSelectedElements().FirstOrDefault();
            if (palka == null || palka is not FamilyInstance)
            {
                message = "Выберите палку!";
                return Result.Cancelled;
            }

            if (!(palka as FamilyInstance).Symbol.FamilyName.Contains("Палка"))
            {
                message = "Выберите палку!";
                return Result.Cancelled;
            }

            var razdel = palka.get_Parameter(new Guid("e1b06433-f527-403c-8986-af9a01e6be7f")).AsString();
            if (razdel.IsNullOrEmpty())
            {
                message = "Укажите раздел в палке!";
                return Result.Cancelled;
            }

            var orFilter = new LogicalOrFilter([new ElementClassFilter(typeof(ViewPlan)), new ElementClassFilter(typeof(ViewSection))]);

            var views = RevitAPI.Document.ToElements<View>(orFilter)
                .Where(v => !v.IsTemplate && v.get_Parameter(new Guid("e1b06433-f527-403c-8986-af9a01e6be7f")).AsString() == razdel && v.Id != palka.OwnerViewId)
                .ToList();

            var viewModel = new PalkaViewManagerViewModel(views, palka);
            var window = new PalkaViewManagerWindow(viewModel);
            viewModel.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}