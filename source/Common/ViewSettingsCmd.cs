using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Common.WPF;
using BIMPlugins.ExtStorage;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewSettingsCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (DockablePane.PaneIsRegistered(ViewSettingWindow.PaneId))
            {
                var pane = RevitAPI.UIApplication.GetDockablePane(ViewSettingWindow.PaneId);
                if (pane.IsShown()) pane.Hide();
                else pane.Show();
            }

            return Result.Succeeded;
        }
    }
}
