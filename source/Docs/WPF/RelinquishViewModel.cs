using BIMPlugins.ExtStorage;
using BIMPlugins.Windows.RevitServer.Classes;
using BIMPlugins.Windows.RevitServer.ViewModels;
using BIMPlugins.Windows.RevitServer.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace BIMPlugins.Docs.WPF
{
    public partial class RelinquishViewModel : ObservableObject
    {
        [ObservableProperty] private string _username = RevitAPI.Application.Username;
        [ObservableProperty] private ObservableCollection<FileItem> _files = [];

        [RelayCommand]
        private void ChooseFromExplorer()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.DefaultExt = ".rvt";
            openFileDialog.Filter = "Проекты Revit|*.rvt";
            openFileDialog.Title = "Выберите проект Revit";

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    var fileItem = new FileItem(Path.GetFileName(file)) { FilePath = file, Version = RevitAPI.Application.VersionNumber};

                    if (!Files.Contains(fileItem))
                        Files.Add(fileItem);
                }
            }
        }

        [RelayCommand]
        private void ChooseFromRSN()
        {
            var vm = new MultiFilesViewModel();
            var window = new MultiFilesWindow(vm);
            vm.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            if (vm.IsCancelled) return;

            foreach (var file in vm.Projects)
            {
                if (!Files.Contains(file))
                    Files.Add(file);
            }
        }

        [RelayCommand]
        private void RemoveProject(FileItem fileItem) => Files.Remove(fileItem);

        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();

            var setFile = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins\relinquish.xml";

            var groupedFiles = Files
                .GroupBy(f => f.Version)
                .OrderBy(g => g.Key)
                .ToList();

            var xDoc = new XDocument(
                new XElement("revitFiles",
                    from versionGroup in groupedFiles
                    select new XElement("version",
                        new XAttribute("name", versionGroup.Key),
                        from file in versionGroup
                        select new XElement("filePath",
                            new XAttribute("path", file.FilePath)
                        )
                    )
                )
            );
            xDoc.Save(setFile);

            foreach (var versionGroup in groupedFiles)
            {
                ChangeUsername(versionGroup.Key);

                string revitPath = @$"C:\Program Files\Autodesk\Revit {versionGroup.Key}\Revit.exe";
                string arguments = "/relinquish";

                var startInfo = new ProcessStartInfo
                {
                    FileName = revitPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                };

                Process.Start(startInfo);
            }
        }

        private void ChangeUsername(string version)
        {
            string path = $@"C:\Users\{Environment.UserName}\AppData\Roaming\Autodesk\Revit\Autodesk Revit {version}\Revit.ini";

            Encoding encoding = GetEncoding(path);

            var lines = File.ReadAllLines(path, encoding);

            string projectPath = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Username="))
                {
                    lines[i] = $"Username={Username}";
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

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}