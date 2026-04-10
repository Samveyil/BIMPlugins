using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd1 : IExternalCommand
    {

        private const double GoldenRatioConjugate = 0.618033988749895;
        private static double _currentHue = 0;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //var doc = RevitAPI.Document;

            //var result = string.Empty;

            //var invalidShParams = doc.ToElements<SharedParameterElement>()
            //    .Where(p => p.GetDefinition().ParameterGroup == BuiltInParameterGroup.INVALID)
            //    .Select(p => p.Name)
            //    .ToList();

            //using (RevitProgressBar progressBar = new RevitProgressBar(true))
            //{
            //    progressBar.Run("Поиск параметра...", doc.ToElements<Family>().Where(f => f.IsUserCreated && !f.IsInPlace), (fam) =>
            //    {
            //        var famDoc = doc.EditFamily(fam);

            //        var parameters = famDoc.FamilyManager.GetParameters()
            //            .Select(p => p.Definition.Name)
            //            .ToList();

            //        var common = parameters.Intersect(invalidShParams).ToList();
            //        if (common.Count > 0)
            //            result += $"{famDoc.Title} " + string.Join(", ", common) + "\n";

            //        famDoc.Close(false);
            //    });

            //    if (progressBar.IsCancelling())
            //    {
            //        return Result.Cancelled;
            //    }
            //}

            //TaskDialog.Show("Инфо", result);

            var paramNames = new List<string>
            {
                "ADSK_Обозначение",
                "ADSK_Код металлопроката",
                "#Арматура_Код металлопроката",
                "#Проволка_Код металлопроката"
            };

            var saveOpt = new SaveAsOptions()
            {
                MaximumBackups = 1
            };

            var famFolderPath = @"C:\Users\shibliev\Desktop\Test";

            foreach (var filePath in Directory.GetFiles(famFolderPath))
            {
                var famDoc = RevitAPI.Application.OpenDocumentFile(filePath);
                //var famDoc = RevitAPI.Document;
                var famManager = famDoc.FamilyManager;

                var parameters = famManager.GetParameters().ToList();

                using (Transaction t = new Transaction(famDoc, "test"))
                {
                    t.Start();

                    //var param = parameters.FirstOrDefault(p => p.Definition.Name == "Класс стали");
                    //famManager.RemoveParameter(param);

                    //if (famManager.Types.Cast<FamilyType>().Count() == 3)
                    //    famManager.DeleteCurrentType();

                    //famManager.RenameCurrentType("А500С");

                    //var newParam = famManager.AddParameter(
                    //    ParameterMethods.FindExternalDefinition("ADSK_Код металлопроката", new Guid("32a47c7f-e91d-4a8e-bf24-927cb679b4d1")),
                    //    BuiltInParameterGroup.PG_REBAR_ARRAY,
                    //    false
                    //);
                    //famManager.Set(newParam, 500);

                    //newParam = famManager.AddParameter("#Арматура_Код металлопроката",
                    //    BuiltInParameterGroup.PG_REBAR_ARRAY,
                    //    ParameterType.Text,
                    //    false
                    //);

                    //famManager.SetDescription(newParam, "240 - А240;\r\n400 - А400;\r\n500 - А500С; 500.1 - В500С; 500.3 - А500; 500.4 - А500СП;\r\n600 - А600; 600.1 - Ап600;\r\n800 - А800;\r\n1000 - А1000;");
                    //famManager.SetFormula(newParam, "\"#\"");

                    //newParam = famManager.AddParameter("#Проволка_Код металлопроката",
                    //    BuiltInParameterGroup.PG_REBAR_ARRAY,
                    //    ParameterType.Text,
                    //    false
                    //);

                    //famManager.SetDescription(newParam, "500.2 - Вp-I;\r\n1200 - Вр-II Ø8; 1200.1 - В-II Ø8;\r\n1300 - Вр-II Ø7; 1300.1 - В-II Ø7;\r\n1400 - Вр-II (Ø4 Ø5 Ø6); 1400.1 - В-II (Ø4 Ø5 Ø6);\r\n1500 - Вр-II Ø3; 1500.1 - В-II Ø3;");
                    //famManager.SetFormula(newParam, "\"#\"");

                    //famDoc.Regenerate();
                    //parameters = famManager.GetParameters().ToList();

                    //var index = parameters.FindIndex(p => p.Definition.Name == "ADSK_Код металлопроката");
                    //param = parameters[index];

                    //parameters.RemoveAt(index);
                    //parameters.Insert(index - 2, param);

                    famManager.ReorderParameters(parameters);

                    t.Commit();
                }

                var newPath = filePath.Replace(".rfa", "1.rfa");
                famDoc.SaveAs(newPath, saveOpt);

                famDoc.Close(false);

                File.Delete(filePath);
                File.Move(newPath, filePath);
            }

            //var view = doc.ActiveView;

            //var filters = view.GetFilters();

            //var patternId = new FilteredElementCollector(RevitAPI.Document)
            //    .OfClass(typeof(FillPatternElement))
            //    .FirstOrDefault(e => e.Name == "<Сплошная заливка>")
            //    .Id;

            //using (Transaction t = new Transaction(doc, "test"))
            //{
            //    t.Start();

            //    foreach (var filterId in filters)
            //    {
            //        view.SetFilterOverrides(filterId, SetPatternColor(GenerateDistinctColor(), patternId));
            //    }

            //    t.Commit();
            //}

            return Result.Succeeded;
        }

        private Color GenerateDistinctColor()
        {
            _currentHue += GoldenRatioConjugate;
            _currentHue %= 1.0;

            double hue = _currentHue * 360;

            double saturation = 0.7;
            double value = 0.9;

            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;

            double r, g, b;

            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return new Color(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }
        private OverrideGraphicSettings SetPatternColor(Color color, ElementId patternId)
        {
            var graphicSettings = new OverrideGraphicSettings();

            graphicSettings.SetSurfaceForegroundPatternColor(color);
            graphicSettings.SetSurfaceForegroundPatternId(patternId);

            return graphicSettings;
        }
    }
}