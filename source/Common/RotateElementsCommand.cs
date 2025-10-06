using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Common.WPF;
using BIMPlugins.Bars;


namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RotateElementsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {            
            var options = new RotateViewModel();
            var view = new RotateView(options);
            RevitOptionsBar.Show(view);

            return Result.Succeeded;
        }
    }
}