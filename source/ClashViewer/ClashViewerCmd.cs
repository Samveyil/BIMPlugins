using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ClashViewer.WPF;

namespace BIMPlugins.ClashViewer
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ClashViewerCmd : IExternalCommand
    {
        private static ClashViewerWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var vm = new ClashViewerVM();

            _window ??= new ClashViewerWindow(vm);

            _window.Show();
            _window.Activate();

            return Result.Succeeded;
        }
    }
}