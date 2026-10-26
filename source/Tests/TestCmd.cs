using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.ExtStorage.Interfaces;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;
            var intUnit = 1d.FromMillimeters();

            //Solid solid = RevitAPI.UIDocument.PickObject("Выбрать solid").ToSolid();

            //var famDoc = RevitAPI.Application.Documents.Cast<Document>().First(d => d.Title == "Подрезка парапета пристроек.rfa");
            //using (Transaction t = new Transaction(famDoc, "Создать FreeForm"))
            //{
            //    t.Start();

            //    var ffe = FreeFormElement.Create(famDoc, solid);
            //    ffe.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING).Set(1);

            //    t.Commit();
            //}

            var rebarFolder = @"\\Diskstation\производство\Ревит\FAMILIES\2019\ОСК\280_2D_Арматура";

            var saveOpt = new SaveAsOptions()
            {
                MaximumBackups = 1
            };

            foreach (var famPath in Directory.GetFiles(@"\\Diskstation\производство\Ревит\FAMILIES\2019\ОСК\285_2D_Армирование стен"))
            {
                var famDoc = RevitAPI.Application.OpenDocumentFile(famPath);

                if (famDoc.ToElements<LinePatternElement>().FirstOrDefault(n => n.Name == "01_Штриховая_ш3 п2") != null)
                {
                    famDoc.Close(false);
                    continue;
                }

                var linePattern = famDoc.ToElements<LinePatternElement>()
                    .FirstOrDefault(n => n.Name == "Линия выравнивания");

                var viewId = famDoc.ToElementIds<ViewPlan>()
                    .FirstOrDefault();

                using (Transaction t = new Transaction(famDoc, "test"))
                {
                    t.Start();

                    linePattern.Name = "01_Штриховая_ш3 п2";

                    var pattern = linePattern.GetLinePattern();
                    var segments = pattern.GetSegments();

                    segments.First().Length = 3d.FromMillimeters();
                    segments.Last().Length = 2d.FromMillimeters();

                    pattern.SetSegments(segments);

                    linePattern.SetLinePattern(pattern);

                    foreach (var nestedFamilyName in famDoc.ToElements<Family>().Select(f => f.Name).Where(n => n.StartsWith("280_")))
                    {
                        famDoc.LoadFamily(rebarFolder.AppendPath(nestedFamilyName) + ".rfa", new FamilyLoadOptions(true), out var family);
                    }

                    t.Commit();
                }

                saveOpt.PreviewViewId = viewId;

                var newPath = famPath.Replace(".rfa", "1.rfa");
                famDoc.SaveAs(newPath, saveOpt);

                famDoc.Close(false);

                File.Delete(famPath);
                File.Move(newPath, famPath);
            }

            return Result.Succeeded;
        }
    }
}
#endif