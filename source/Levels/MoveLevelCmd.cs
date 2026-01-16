using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.Levels.WPF;

namespace BIMPlugins.Levels
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MoveLevelCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var options = new MoveLevelViewModel();
            var view = new MoveLevelView(options);
            RevitOptionsBar.Show(view);

            return Result.Succeeded;
        }
    }
}