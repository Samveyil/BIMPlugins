using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using BIMPlugins.Windows.RevitServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace BIMPlugins.Docs
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RSNInfoCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var files = RSN.SelectFiles();
            if (files == null || files.Count == 0)
                return Result.Cancelled;

            var isDetached = MessageWindow.ShowMessage("Отсоединить модель?", buttons: System.Windows.MessageBoxButton.YesNo, icon: System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

            var setFile = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins\openDocs.xml";

            var groupedFiles = files
                .GroupBy(f => f.Version)
                .OrderBy(g => g.Key)
                .ToList();

            bool openInSameSession = false;

            var sameVersionGroup = groupedFiles.FirstOrDefault(g => g.Key == RevitAPI.Application.VersionNumber);
            if (sameVersionGroup != null)
            {
                if (MessageWindow.ShowMessage("Открыть в текущем сеансе?", buttons: System.Windows.MessageBoxButton.YesNo, icon: System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes)
                {
                    groupedFiles.Remove(sameVersionGroup);
                    openInSameSession = true;
                }
            }

            var xDoc = new XDocument(
                new XElement("revitFiles",
                    from versionGroup in groupedFiles
                    select new XElement("version",
                        new XAttribute("name", versionGroup.Key),
                        from file in versionGroup
                        select new XElement("filePath",
                            new XAttribute("path", file.Path)
                        )
                    ),
                    new XElement("settings",
                        new XAttribute("isDetached", isDetached)
                    )
                )
            );
            xDoc.Save(setFile);

            foreach (var versionGroup in groupedFiles)
            {
                string revitPath = @$"C:\Program Files\Autodesk\Revit {versionGroup.Key}\Revit.exe";
                string arguments = "/openDocs";

                var startInfo = new ProcessStartInfo
                {
                    FileName = revitPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                };

                Process.Start(startInfo);
            }

            if (openInSameSession)
            {
                var uiApp = RevitAPI.UIApplication;
                uiApp.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(PartitionsDialogBoxHide);

                // A file that cannot be opened does not stop the others; why it failed is shown at the end.
                var errors = new List<string>();
                try
                {
                    foreach (var file in sameVersionGroup)
                    {
                        var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(file.Path);

                        string error;
                        var uiDoc = isDetached
                            ? mPath.OpenAndActivateDetachedDocument(out error)
                            : mPath.OpenAndActivateLocalDocument(out error);

                        if (uiDoc == null)
                            errors.Add(error);
                    }
                }
                finally
                {
                    uiApp.DialogBoxShowing -= new EventHandler<DialogBoxShowingEventArgs>(PartitionsDialogBoxHide);
                }

                if (errors.Count != 0)
                    ReportWindow.ShowReport(errors, "Не удалось открыть");
            }

            return Result.Succeeded;
        }

        private void PartitionsDialogBoxHide(object sender, DialogBoxShowingEventArgs e)
        {
            if (e.DialogId.Contains("Dialog_Revit_Partitions"))
                e.OverrideResult((int)System.Windows.Forms.DialogResult.Yes);
        }
    }
}