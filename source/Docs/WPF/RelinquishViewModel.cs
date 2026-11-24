using BIMPlugins.ExtStorage;
using BIMPlugins.Windows.RevitServer;
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
        [ObservableProperty] private ObservableCollection<RelinquishFile> _files = [];

        [RelayCommand]
        private void ChooseFromExplorer()
        {
            var version = RevitAPI.Application.VersionNumber;

            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.DefaultExt = ".rvt";
            openFileDialog.Filter = "Проекты Revit|*.rvt";
            openFileDialog.Title = "Выберите проект Revit";

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var file in openFileDialog.FileNames)
                {
                    var fileItem = new RelinquishFile(file, version);

                    if (!Files.Contains(fileItem))
                        Files.Add(fileItem);
                }
            }
        }

        [RelayCommand]
        private void ChooseFromRSN()
        {
            var files = RSN.SelectFiles();
            if (files == null) return;

            foreach (var file in files)
            {
                var fileItem = new RelinquishFile(file.Path, file.Version);

                if (!Files.Contains(fileItem))
                    Files.Add(fileItem);
            }
        }

        [RelayCommand]
        private void RemoveProject(RelinquishFile fileItem) => Files.Remove(fileItem);

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

            var lines = File.ReadAllLines(path, encoding).ToList();

            int usernameIndex = lines.FindIndex(line =>
                line.TrimStart().StartsWith(
                    "Username=",
                    StringComparison.OrdinalIgnoreCase));

            if (usernameIndex >= 0)
            {
                lines[usernameIndex] = $"Username={Username}";
            }
            else
            {
                int frequencyIndex = lines.FindIndex(line =>
                    line.TrimStart().StartsWith(
                        "WorksharingDisplayFrequency=",
                        StringComparison.OrdinalIgnoreCase));

                if (frequencyIndex < 0)
                {
                    throw new InvalidOperationException(
                        "WorksharingDisplayFrequency was not found in Revit.ini.");
                }

                lines.Insert(frequencyIndex + 1, $"Username={Username}");
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

    /// <summary>A project to relinquish: its path, local or RSN://, and the Revit version to open it in.</summary>
    public sealed class RelinquishFile(string filePath, string version) : IEquatable<RelinquishFile>
    {
        public string FilePath { get; } = filePath;
        public string Version { get; } = version;

        public bool Equals(RelinquishFile other) =>
            other != null && string.Equals(FilePath, other.FilePath, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) => Equals(obj as RelinquishFile);
        public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath);
    }
}
