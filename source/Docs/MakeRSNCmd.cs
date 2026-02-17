using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace BIMPlugins.Docs
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MakeRSNCmd : IExternalCommand
    {
        private Dictionary<string, List<string>> serverIPDict = new Dictionary<string, List<string>>()
        {
            {"RSN2019", new List<string>()
                {
                    "srv1",
                    "srv2",
                    "srv4",
                    "extrevit",
                    "extrevit3",
                    "extrevit4",
                    "extrevit5",
                    "extrevit6",
                    "extrevit7",
                    "extrevit8"
                }
            },
            {"RSN2020", new List<string>()
                {
                    "srv2",
                    "srv4",
                    "extrevit6",
                    "extrevit9"
                }
            },
            {"RSN2021", new List<string>()
                {
                    "srv1",
                    "srv4"
                }
            },
            {"RSN2022", new List<string>()
                {
                    "srv2",
                    "srv4",
                    "extrevit2",
                    "extrevit10",
                    "extrevit11"
                }
            },
            {"RSN2023", new List<string>()
                {
                    "srv4",
                    "extrevit3"
                }
            }
        };

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var directoryPath = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\BIMPlugins";
            Directory.CreateDirectory(directoryPath);

            string xmlOutputPath = directoryPath.AppendPath("Структура RevitServer.xml");
            
            XDocument doc = new XDocument(new XElement("RevitServer"));
            foreach (KeyValuePair<string, List<string>> kvp in serverIPDict)
            {
                var version = new XElement(kvp.Key);
                foreach (string IP in kvp.Value)
                {
                    string sourceDirectory;

                    if (kvp.Key == "RSN2019")
                    {
                        sourceDirectory = IP switch
                        {
                            "srv1" or "srv4" => @$"\\{IP}\Projects19",
                            "srv2" => @$"\\{IP}\Projects_2019",
                            _ => @$"\\{IP}\Projects"
                        };
                    }
                    else if (kvp.Key == "RSN2020")
                    {
                        sourceDirectory = IP switch
                        {
                            "srv2" => @$"\\{IP}\Projects",
                            _ => @$"\\{IP}\Projects20"
                        };
                    }
                    else if (kvp.Key == "RSN2021")
                    {
                        sourceDirectory = @$"\\{IP}\Projects21";
                    }
                    else if (kvp.Key == "RSN2022")
                    {
                        sourceDirectory = IP switch
                        {
                            "extrevit10" => @$"\\{IP}\Projects",
                            _ => @$"\\{IP}\Projects22"
                        };
                    }
                    else
                    {
                        sourceDirectory = IP switch
                        {
                            "extrevit3" => @$"\\{IP}\Projects 2023",
                            _ => @$"\\{IP}\Projects23"
                        };
                    }

                    if (!Directory.Exists(sourceDirectory))
                        continue;

                    XElement targetNode = new XElement("server", new XAttribute("name", IP));
                    GetRvtFiles(sourceDirectory, targetNode, true);

                    version.Add(targetNode);
                }
                doc.Root.Add(version);
            }

            doc.Save(xmlOutputPath);
            return Result.Succeeded;
        }

        private void GetRvtFiles(string sourceDir, XElement parentElement, bool isRoot = false)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly).OrderBy(d => d).ToList())
            {
                string dirName = new DirectoryInfo(dirPath).Name;
                if (dirName.StartsWith("_"))
                    continue;
                
                XElement dirElement;
                if (dirName.EndsWith(".rvt"))
                    dirElement = new XElement("file", new XAttribute("name", dirName));
                else if (isRoot)
                    dirElement = new XElement("project", new XAttribute("name", dirName));
                else
                    dirElement = new XElement("folder", new XAttribute("name", dirName));

                if (!dirName.EndsWith(".rvt"))
                {
                    foreach (string filePath in Directory.GetFiles(dirPath).Where(f => f.EndsWith(".rvt")))
                    {
                        XElement fileElement = new XElement("file", new XAttribute("name", Path.GetFileName(filePath)));
                        dirElement.Add(fileElement);
                    }

                    GetRvtFiles(dirPath, dirElement);
                }

                parentElement.Add(dirElement);
            }
        }
    }
}