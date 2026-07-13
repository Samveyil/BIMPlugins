using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Documents;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ColumnWidthFromPanelCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var famSymbol = new ElementId(11544434).ToElement<FamilySymbol>();
            var level = new ElementId(1610).ToElement<Level>();

            //var panels = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_CurtainWallPanels)
            //    .OfType<Panel>()
            //    .Where(p => p.Name == "106_Панель_Стемалит 46(-25)" && p.WorksetId.IntegerValue != 16781)
            //    .ToList();

            var panels = RevitAPI.UIDocument.ToSelectedElements()
                .OfType<Panel>()
                .Where(p => p.Name == "106_Панель_Стемалит 46(-25)" && p.WorksetId.IntegerValue != 16781)
                .ToList();

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            using (Transaction t = new Transaction(doc, "Создать фахверк"))
            {
                t.Start();

                famSymbol.Activate();

                foreach (Panel panel in panels)
                {
                    var solid = panel.ToSolid();
                    if (solid == null)
                        continue;

                    var host = panel.Host as CurtainSystem;
                    var curtainGrid = host.CurtainGrids.Cast<CurtainGrid>().FirstOrDefault();

                    ElementId uGridLineId = ElementId.InvalidElementId;
                    ElementId vGridLineId = ElementId.InvalidElementId;
                    panel.GetRefGridLines(ref uGridLineId, ref vGridLineId);

                    if (vGridLineId == ElementId.InvalidElementId)
                        continue;

                    var uGridLine = curtainGrid.GetUGridLineIds().FirstOrDefault()?.ToElement<CurtainGridLine>();
                    var vGridLines = curtainGrid.GetVGridLineIds().Select(v => v.ToElement<CurtainGridLine>()).ToList();

                    var cell = curtainGrid.GetCell(uGridLineId, vGridLineId);
                    var curves = cell.CurveLoops.Cast<CurveArray>().FirstOrDefault()
                        .Cast<Curve>()
                        .OrderByDescending(c => c.Length)
                        .Take(2)
                        .ToList();

                    foreach (var curve in curves)
                    {
                        ExMethods.CreateDirectShape([curve]);
                        ExMethods.CreateDirectShape([Point.Create(curves[1].GetEndPoint(0))]);
                        ExMethods.CreateDirectShape([Point.Create(curves[0].Project(curves[1].GetEndPoint(0)).XYZPoint)]);
                    }

                    var dist1 = curves[0].Distance(curves[1].GetEndPoint(0)) * 304.8;
                    var dist2 = curves[0].Project(curves[1].GetEndPoint(0)).Distance * 304.8;

                    Debug.WriteLine(dist1.ToString());
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif