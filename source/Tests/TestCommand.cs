using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var detailCompType = RevitAPI.Document
                .ToElements<ElementType>()
                .Where(c => c.FamilyName == "Последовательность узлов")
                .FirstOrDefault(c => c.Name == "Кирпич");

            var graphStyle = RevitAPI.Document.ToElements<GraphicsStyle>().FirstOrDefault(e => e.Name == "Элементы узлов");

            using (var t = new Transaction(RevitAPI.Document, "Создание последовательности узлов"))
            {
                t.Start();

                var line = Line.CreateBound(new XYZ(0, 0, 0), new XYZ(10, 0, 0));
                line.SetGraphicsStyleId(graphStyle.Id);

                var detailCurve = RevitAPI.Document.Create.NewDetailCurve(RevitAPI.ActiveView, line);
                //detailCurve.LineStyle = graphStyle;

                //detailCurve.ChangeTypeId(new ElementId(50949));

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
