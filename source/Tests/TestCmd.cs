using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            var famSymbol = new ElementId(26766366).ToElement<FamilySymbol>();
            var level = new ElementId(3090007).ToElement<Level>();

            var linkInstance = new ElementId(25664072).ToElement<RevitLinkInstance>();
            var linkDoc = linkInstance.GetLinkDocument();
            var transform = linkInstance.GetTotalTransform();

            var panels = linkDoc.ToElements<FamilyInstance>(BuiltInCategory.OST_CurtainWallPanels)
                .OfType<Panel>()
                .Where(p => p.Name == "106_Панель_Стемалит 46(-25)")
                .ToList();

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            var angles = new List<string>();

            using (Transaction t = new Transaction(doc, "Создать фахверк"))
            {
                t.Start();

                famSymbol.Activate();

                foreach (var panel in panels)
                {
                    var centroid = panel.ToSolid()?.ComputeCentroid();
                    if (centroid == null)
                        continue;

                    ElementId uGridLineId = ElementId.InvalidElementId;
                    ElementId vGridLineId = ElementId.InvalidElementId;
                    panel.GetRefGridLines(ref uGridLineId, ref vGridLineId);

                    if (vGridLineId == ElementId.InvalidElementId)
                        continue;

                    var gridLine = vGridLineId.ToElement<CurtainGridLine>(linkDoc);
                    var line = gridLine.AllSegmentCurves.Cast<Line>().FirstOrDefault();

                    var centerLine = Line.CreateBound(
                        centroid + line.Direction.Normalize() * -line.Length / 2,
                        centroid + line.Direction.Normalize() * line.Length / 2
                    );

                    var panelFaceOrient = panel.FacingOrientation.Normalize();
                    var translation = panelFaceOrient * 500 * intUnit;

                    XYZ startPoint = centerLine.GetEndPoint(0);
                    XYZ endPoint = centerLine.GetEndPoint(1);

                    var orig = new XYZ(startPoint.X + translation.X, startPoint.Y + translation.Y, startPoint.Z);
                    var newLine = Line.CreateBound(
                        orig,
                        new XYZ(endPoint.X + translation.X, endPoint.Y + translation.Y, endPoint.Z)
                    );

                    var colAxis = newLine.CreateTransformed(transform) as Line;
                    
                    var slantedColumn = doc.Create.NewFamilyInstance(
                        colAxis,
                        famSymbol,
                        level,
                        StructuralType.Column
                    );

                    doc.Regenerate();
                    
                    slantedColumn.get_Parameter(BuiltInParameter.SLANTED_COLUMN_BASE_CUT_STYLE).Set(1);
                    slantedColumn.get_Parameter(BuiltInParameter.SLANTED_COLUMN_TOP_CUT_STYLE).Set(1);

                    var columnFaceOrient = slantedColumn.FacingOrientation.Normalize();

                    var colLine = Line.CreateBound(orig, orig + new XYZ(columnFaceOrient.X * 300 * intUnit, columnFaceOrient.Y * 300 * intUnit, 0));
                    var panelLine = Line.CreateBound(startPoint, startPoint + new XYZ(panelFaceOrient.X * 300 * intUnit, panelFaceOrient.Y * 300 * intUnit, 0))
                        .CreateTransformed(transform) as Line;

                    //ExMethods.CreateDirectShape([colLine, panelLine]);

                    var angle = colLine.Direction.Normalize().AngleTo(panelLine.Direction.Normalize());

                    slantedColumn.get_Parameter(new Guid("4f9a558c-61b9-4c38-a08c-a25465aa8abd")).Set(angle);

                    ElementTransformUtils.RotateElement(doc, slantedColumn.Id,
                        colAxis,
                        angle
                    );
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif