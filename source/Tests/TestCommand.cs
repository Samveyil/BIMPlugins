using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var directShapes = doc.ToElements<DirectShape>().ToList();

            var columns = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_StructuralColumns).Where(c => c.Location is LocationCurve);

            var floors = doc.ToElements<Floor>(BuiltInCategory.OST_Floors).Where(f => f.FloorType.Name.EndsWith("мм"));
            var floorsTopFace = floors
                .Select(f => f.ToSolid()?.Faces.OfType<PlanarFace>().FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(new XYZ(0, 0, 1))))
                .Where(face => face != null);
            var floorsTopFaceZCoord = floorsTopFace
                .Select(f => f.Origin.Z)
                .OrderBy(f => f)
                .ToList();

            var worksetId = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).FirstOrDefault(w => w.Name == "02_Опалубка").Id;

            using (TransactionGroup tGroup = new TransactionGroup(doc, "Создать/Обновить точки"))
            {
                tGroup.Start();

                using (Transaction t = new Transaction(RevitAPI.Document, "Удаление/Обновление отметок DS"))
                {
                    t.Start();

                    foreach (var ds in directShapes.ToList())
                    {
                        int.TryParse(ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString(), out var columnId);

                        if (new ElementId(columnId).ToElement() == null)
                        {
                            doc.Delete(ds.Id);
                            directShapes.Remove(ds);
                        }
                        else
                        {
                            var dsLocation = ds.get_BoundingBox(null).Max;
                            var dsLocationZ = dsLocation.Z;
                            if (!floorsTopFaceZCoord.Select(f => f.Round()).Contains(dsLocationZ.Round()))
                            {
                                double closest = 0;
                                double minDiff = 1000000;
                                foreach (var faceLocation in floorsTopFaceZCoord)
                                {
                                    var diff = Math.Abs(dsLocationZ - faceLocation);
                                    if (diff < minDiff)
                                    {
                                        minDiff = diff;
                                        closest = faceLocation;
                                    }
                                }

                                ElementTransformUtils.MoveElement(doc, ds.Id, new XYZ(dsLocation.X, dsLocation.Y, closest) - dsLocation);
                            }
                        }
                    }

                    t.Commit();
                }

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Генерация точек...", columns, (column) =>
                    {
                        var columnDS = directShapes
                            .Where(d => d.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString() == column.Id.ToString())
                            .ToList();
                        
                        var columnCurve = (column.Location as LocationCurve).Curve;

                        foreach (var floorTopFace in floorsTopFace)
                        {
                            floorTopFace.Intersect(columnCurve, out var result);
                            if (result == null)
                                continue;

                            var intPoint = result.Cast<IntersectionResult>().FirstOrDefault()?.XYZPoint;
                            if (intPoint == null)
                                continue;

                            using (Transaction t = new Transaction(RevitAPI.Document, "Генерация DS"))
                            {
                                t.Start();

                                var ds = columnDS.FirstOrDefault(d => d.get_BoundingBox(null).Max.Z.Round() == floorTopFace.Origin.Z.Round());
                                if (ds == null)
                                {
                                    ds = ExMethods.CreateDirectShape([Point.Create(intPoint)], BuiltInCategory.OST_BridgeCables);
                                    columnDS.Add(ds);
                                }
                                else
                                {
                                    ElementTransformUtils.MoveElement(doc, ds.Id, intPoint - ds.get_BoundingBox(null).Max);
                                }

                                ds.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(column.Id.ToString());
                                ds.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM).Set(worksetId.IntegerValue);

                                t.Commit();
                            }
                        }

                        using (Transaction t = new Transaction(RevitAPI.Document, "Удаление старых DS колонны"))
                        {
                            t.Start();

                            foreach (var ds in columnDS)
                            {
                                var dsLoc = ds.get_BoundingBox(null).Max;

                                XYZ projectedPoint = columnCurve.Project(dsLoc).XYZPoint;

                                double distance = dsLoc.DistanceTo(projectedPoint);
                                if (distance.Round() != 0)
                                {
                                    doc.Delete(ds.Id);
                                    directShapes.Remove(ds);
                                }
                            }

                            t.Commit();
                        }  
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        tGroup.RollBack();
                        return Result.Cancelled;
                    }
                }

                tGroup.Assimilate();
            }

            return Result.Succeeded;
        }
    }
}