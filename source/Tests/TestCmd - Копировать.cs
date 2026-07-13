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
using System.Windows.Documents;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd1 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var famSymbol = new ElementId(11544434).ToElement<FamilySymbol>();
            var level = new ElementId(1610).ToElement<Level>();

            //var panels = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_CurtainWallPanels)
            //    .OfType<Panel>()
            //    .Where(p => p.Name == "106_Панель_Стемалит 46(-25)")
            //    .ToList();

            var panels = RevitAPI.UIDocument.ToSelectedElements();

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            using (Transaction t = new Transaction(doc, "Создать фахверк"))
            {
                t.Start();

                famSymbol.Activate();

                foreach (Panel panel in panels)
                {
                    var host = panel.Host as CurtainSystem;
                    var curtainGrid = host.CurtainGrids.Cast<CurtainGrid>().FirstOrDefault();

                    var mullions = curtainGrid.GetMullionIds().Select(id => id.ToElement<Mullion>()).ToList();

                    ElementId uGridLineId = ElementId.InvalidElementId;
                    ElementId vGridLineId = ElementId.InvalidElementId;
                    panel.GetRefGridLines(ref uGridLineId, ref vGridLineId);

                    if (vGridLineId == ElementId.InvalidElementId)
                        continue;

                    //var cell = curtainGrid.GetCell(uGridLineId, vGridLineId);
                    //var curveArray = cell.CurveLoops.Cast<CurveArray>().FirstOrDefault();

                    //var mullionsForDelete = new List<ElementId>();
                    //var mullionOrigins = new List<XYZ>();
                    //foreach (var curve in curveArray)
                    //{
                    //    if (curve is Line line)
                    //    {
                    //        var mullion = mullions.FirstOrDefault(m => (m.LocationCurve as Line).Origin.IsAlmostEqualTo(line.Origin));
                    //        if (mullion != null)
                    //        {
                    //            mullion.Lock = false;
                    //            mullionsForDelete.Add(mullion.Id);
                    //            mullionOrigins.Add(line.Origin);
                    //        }
                    //    }
                    //}

                    var gridLine = vGridLineId.ToElement<CurtainGridLine>();
                    var lineOrigin = (gridLine.FullCurve as Line).Origin;

                    var mullion = mullions.FirstOrDefault(m => (m.LocationCurve as Line).Origin.IsAlmostEqualTo(lineOrigin));
                    if (mullion != null)
                    {
                        mullion.Lock = false;
                        doc.Delete(mullion.Id);
                    }

                    doc.Regenerate();

                    //var uGridLineIds = curtainGrid.GetUGridLineIds().ToList();
                    var vGridLines = curtainGrid.GetVGridLineIds().Select(v => v.ToElement<CurtainGridLine>()).ToList();

                    var normal = panel.FacingOrientation.Normalize();

                    var solid = panel.ToSolid();
                    if (solid == null)
                        continue;

                    var face = solid.Faces.Cast<PlanarFace>().FirstOrDefault(f => f.FaceNormal.Normalize().IsAlmostEqualTo(normal));
                    var loop = face.GetEdgesAsCurveLoops().First();

                    var count = loop.NumberOfCurves();
                    var isClockWise = loop.IsCounterclockwise(normal);

                    var offsetLoop = CurveLoop.CreateViaOffset(loop, [-26 * intUnit, -18 * intUnit, -25 * intUnit, 0], normal);

                    var newSolid = GeometryCreationUtilities.CreateExtrusionGeometry([offsetLoop], normal, 200 * intUnit);

                    ExMethods.CreateDirectShape([newSolid], BuiltInCategory.OST_Walls);

                    var mullType = new ElementId(12935613).ToElement<MullionType>();
                    //foreach (var origin in mullionOrigins)
                    //{
                    //    var gridLine = vGridLines.FirstOrDefault(v => (v.FullCurve as Line).Origin.IsAlmostEqualTo(origin));
                    //    if (gridLine != null)
                    //    {
                    //        var set = gridLine.AddMullions(gridLine.FullCurve, mullType, true);
                    //        foreach (var mull in set.Cast<Mullion>().ToList())
                    //        {
                    //            mull.Lock = true;
                    //        }
                    //    }
                    //}

                    var set = gridLine.AddMullions(gridLine.FullCurve, mullType, true);
                    foreach (var mull in set.Cast<Mullion>().ToList())
                    {
                        mull.Lock = true;
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif