using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
    public class RSNInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var vm = new MultiFilesViewModel();
            var window = new MultiFilesWindow(vm);

            vm.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            if (vm.IsCancelled)
                return Result.Cancelled;

            var isDetached = MessageWindow.ShowMessage("Отсоединить модель?", System.Windows.MessageBoxImage.Question, false);

            var setFile = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins\openDocs.xml";

            var groupedFiles = vm.Projects
                .GroupBy(f => f.Version)
                .OrderBy(g => g.Key);

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
                        new XAttribute("isDetached", isDetached == System.Windows.MessageBoxResult.Yes)
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

            return Result.Succeeded;
        }
    }
}