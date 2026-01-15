using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(@"RSN://srv4/Зюзино 4.1/02_КР/OLP_R19_ZUZN4.1_PD_01_KR_K00.rvt");

            RevitAPI.UIApplication.DialogBoxShowing += new EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs>(DialogBoxHide);

            mPath.OpenAndActivateLocalDocument(@"RSN://srv4/Зюзино 4.1/02_КР/OLP_R19_ZUZN4.1_PD_01_KR_K00.rvt");

            RevitAPI.UIApplication.DialogBoxShowing -= new EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs>(DialogBoxHide);

            return Result.Succeeded;
        }

        public static void DialogBoxHide(object sender, DialogBoxShowingEventArgs e)
        {
            if (e.DialogId.Contains("Dialog_Revit_Partitions"))
            {
                e.OverrideResult((int)System.Windows.Forms.DialogResult.Yes);
            }
        }
    }
}