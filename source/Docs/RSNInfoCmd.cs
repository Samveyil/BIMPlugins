using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using BIMPlugins.Windows.RevitServer.ViewModels;
using BIMPlugins.Windows.RevitServer.Windows;
using System;
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
            var vm = new MultiFilesViewModel();
            var window = new MultiFilesWindow(vm);

            vm.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            if (vm.IsCancelled)
                return Result.Cancelled;

            var isDetached = MessageWindow.ShowMessage("Отсоединить модель?", System.Windows.MessageBoxImage.Question, false) == System.Windows.MessageBoxResult.Yes;

            var setFile = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins\openDocs.xml";

            var groupedFiles = vm.Projects
                .GroupBy(f => f.Version)
                .OrderBy(g => g.Key)
                .ToList();

            bool openInSameSession = false;

            var sameVersionGroup = groupedFiles.FirstOrDefault(g => g.Key == RevitAPI.Application.VersionNumber);
            if (sameVersionGroup != null)
            {
                if (MessageWindow.ShowMessage("Открыть в текущем сеансе?", System.Windows.MessageBoxImage.Question, false) == System.Windows.MessageBoxResult.Yes)
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
                            new XAttribute("path", file.FilePath)
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
                RevitAPI.UIApplication.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(PartitionsDialogBoxHide);

                if (isDetached)
                {
                    foreach (var file in sameVersionGroup)
                    {
                        var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(file.FilePath);
                        mPath.OpenAndActivateDetachedDocument();
                    }
                }
                else
                {
                    foreach (var file in sameVersionGroup)
                    {
                        var mPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(file.FilePath);
                        mPath.OpenAndActivateLocalDocument(file.FilePath);
                    }
                }

                RevitAPI.UIApplication.DialogBoxShowing -= new EventHandler<DialogBoxShowingEventArgs>(PartitionsDialogBoxHide);
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