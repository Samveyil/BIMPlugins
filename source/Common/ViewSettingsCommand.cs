using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.Common.WPF;


namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewSettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = RevitAPI.UIApplication;

            if (DockablePane.PaneIsRegistered(ViewSettingWindow.PaneId))
            {
                DockablePane myPane = uiapp.GetDockablePane(ViewSettingWindow.PaneId);
                if (myPane.IsShown())
                {
                    myPane.Hide();
                }
                else
                {
                    myPane.Show();
                }
            }

            return Result.Succeeded;
        } 
    }
}