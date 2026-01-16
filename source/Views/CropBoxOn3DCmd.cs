using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Views.WPF;


namespace BIMPlugins.Views
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CropBoxOn3DCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var viewModel = new CropBoxViewModel();
            var window = new CropBoxWindow(viewModel);

            window.Show();

            return Result.Succeeded;
        }
    }
}