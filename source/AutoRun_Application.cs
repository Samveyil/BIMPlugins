using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BIMPlugins
{
    public class AutoRun_Application : IExternalApplication
    {
        public static UIControlledApplication _uiControlApp;

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            _uiControlApp = application;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Contains(@"/openDocs"))
            {
                _uiControlApp.Idling += new EventHandler<IdlingEventArgs>(OpenDocs);
            }

            return Result.Succeeded;
        }

        private void OpenDocs(object sender, IdlingEventArgs e)
        {
            RevitAPI.UIApplication.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(PartitionsDialogBoxHide);

            var setFile = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins\openDocs.xml";
            if (File.Exists(setFile))
            {
                var xDoc = XDocument.Load(setFile);

                if (xDoc.Descendants("settings").FirstOrDefault().Attribute("isDetached").Value == "true")
                {
                    foreach (var xFile in xDoc.Descendants("version").FirstOrDefault(v => v.Attribute("name").Value == RevitAPI.Application.VersionNumber).Elements())
                    {
                        var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(xFile.Attribute("path").Value);
                        mPath.OpenAndActivateDetachedDocument();
                    }
                }
                else
                {
                    foreach (var xFile in xDoc.Descendants("version").FirstOrDefault(v => v.Attribute("name").Value == RevitAPI.Application.VersionNumber).Elements())
                    {
                        var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(xFile.Attribute("path").Value);
                        mPath.OpenAndActivateLocalDocument(xFile.Attribute("path").Value);
                    }
                }
            }
            
            RevitAPI.UIApplication.DialogBoxShowing -= new EventHandler<DialogBoxShowingEventArgs>(PartitionsDialogBoxHide);
            _uiControlApp.Idling -= new EventHandler<IdlingEventArgs>(OpenDocs);
        }

        private void PartitionsDialogBoxHide(object sender, DialogBoxShowingEventArgs e)
        {
            if (e.DialogId.Contains("Dialog_Revit_Partitions"))
                e.OverrideResult((int)System.Windows.Forms.DialogResult.Yes);
        }
    }
}