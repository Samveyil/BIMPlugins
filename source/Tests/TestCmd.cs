using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using System;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd : IExternalCommand
    {

        private const double GoldenRatioConjugate = 0.618033988749895;
        private static double _currentHue = 0;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            //var result = string.Empty;

            //using (RevitProgressBar progressBar = new RevitProgressBar(true))
            //{
            //    progressBar.Run("Поиск параметра...", doc.ToElements<Family>().Where(f => f.IsUserCreated && !f.IsInPlace), (fam) =>
            //    {
            //        var famDoc = doc.EditFamily(fam);

            //        var param = famDoc.FamilyManager.GetParameters().FirstOrDefault(p => p.Definition.Name == "Рзм.ТолщинаОсновы");

            //        if (param != null)
            //            result += fam.Name + "\n";

            //        famDoc.Close(false);
            //    });

            //    if (progressBar.IsCancelling())
            //    {
            //        return Result.Cancelled;
            //    }
            //}

            //TaskDialog.Show("Инфо", result);

            var view = doc.ActiveView;

            var filters = view.GetFilters();

            var patternId = new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(FillPatternElement))
                .FirstOrDefault(e => e.Name == "<Сплошная заливка>")
                .Id;

            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                foreach (var filterId in filters)
                {
                    view.SetFilterOverrides(filterId, SetPatternColor(GenerateDistinctColor(), patternId));
                }

                t.Commit();
            }

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