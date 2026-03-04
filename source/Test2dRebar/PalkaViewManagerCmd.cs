using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Test2dRebar.Classes;
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
            var palkas = RevitAPI.UIDocument.ToSelectedElements()
                .Where(p => p is FamilyInstance instance && instance.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString().StartsWith("285"))
                .ToList();
            if (palkas.Count == 0)
            {
                message = "Выберите палки!";
                return Result.Cancelled;
            }

            var razdel = palkas.Select(p => p.get_Parameter(RebarMethods.RazdelGuid).AsString()).FirstOrDefault(r => !r.IsNullOrEmpty());
            if (razdel.IsNullOrEmpty())
            {
                message = "Укажите раздел в палках!";
                return Result.Cancelled;
            }

            if (!palkas.All(p => p.get_Parameter(RebarMethods.RazdelGuid).AsString() == razdel))
            {
                message = "Разделы в выбранных палках не совпадают!";
                return Result.Cancelled;
            }

            var views = RevitAPI.Document.ToElements<ViewSection>(/*new ElementMulticlassFilter([typeof(ViewPlan), typeof(ViewSection)])*/)
                .Where(v => !v.IsTemplate && v.get_Parameter(new Guid("e1b06433-f527-403c-8986-af9a01e6be7f")).AsString() == razdel)
                .ToList();

            var viewModel = new PalkaViewManagerViewModel(views, palkas);
            var window = new PalkaViewManagerWindow(viewModel);
            viewModel.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}