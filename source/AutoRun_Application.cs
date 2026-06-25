using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

            string command = Environment.GetCommandLineArgs().FirstOrDefault(arg => arg.StartsWith("/"));

            switch (command)
            {
                case "/openDocs":
                    _uiControlApp.Idling += new EventHandler<IdlingEventArgs>(OpenDocs);
                    break;
                case "/relinquish":
                    _uiControlApp.Idling += new EventHandler<IdlingEventArgs>(Relinquish);
                    break;
               
                default:
                    break;
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
        
        private void Relinquish(object sender, IdlingEventArgs e)
        {
            var setFile = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins\relinquish.xml";
            if (File.Exists(setFile))
            {
                var xDoc = XDocument.Load(setFile);

                var toShow = new List<string>();
                foreach (var xFile in xDoc.Descendants("version").FirstOrDefault(v => v.Attribute("name").Value == RevitAPI.Application.VersionNumber).Elements())
                {
                    var filePath = xFile.Attribute("path").Value;

                    var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                    var doc = mPath.OpenLocalDocument(filePath);

                    var elements = WorksharingUtils.RelinquishOwnership(doc, new RelinquishOptions(true), null);
                    if (elements.GetRelinquishedWorksets().Count != 0 || elements.GetRelinquishedElements().Count != 0)
                        toShow.Add("Элементы освобождены!");
                    else
                    {
                        doc.SynchronizeWithCentral("Освобождение элементов за пользователя");
                        toShow.Add("Элементы освобождены при помощи синхронизации!");
                    }

                    if (doc.Close(false))
                        toShow.Add($"{filePath} закрыт\n");
                }

                RechangeBackUsername();

                var reportWindow = new ReportWindow(toShow);
                reportWindow.ShowDialog();

                int intRevitProcID = Process.GetCurrentProcess().Id;
                Process prs = Process.GetProcessById(intRevitProcID);
                prs.Kill();
            }

            _uiControlApp.Idling -= new EventHandler<IdlingEventArgs>(Relinquish);
        }
        private void RechangeBackUsername()
        {
            string path = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Autodesk\Revit\Autodesk Revit {RevitAPI.Application.VersionNumber}\Revit.ini";

            Encoding encoding = GetEncoding(path);

            var lines = File.ReadAllLines(path, encoding);

            string projectPath = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Username="))
                {
                    lines[i] = $"Username={Environment.UserName}";
                    break;
                }
            }

            File.WriteAllLines(path, lines, encoding);
        }
        private Encoding GetEncoding(string filePath)
        {
            using (var reader = new StreamReader(filePath, true))
            {
                reader.Peek();
                return reader.CurrentEncoding;
            }
        }

        private void PartitionsDialogBoxHide(object sender, DialogBoxShowingEventArgs e)
        {
            if (e.DialogId.Contains("Dialog_Revit_Partitions"))
                e.OverrideResult((int)System.Windows.Forms.DialogResult.Yes);
        }
    }
}