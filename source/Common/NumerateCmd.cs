using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.Common.WPF;


namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class NumerateCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var options = new NumerateViewModel();

            if (options.Parameters.Count == 0) return Result.Cancelled;

            var view = new NumerateView(options);
            RevitOptionsBar.Show(view);

            return Result.Succeeded;
        }
    }
}