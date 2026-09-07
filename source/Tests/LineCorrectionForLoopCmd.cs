using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.Methods;
using System.Collections.Generic;
using System.Linq;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LineCorrectionForLoopCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;
            var intUnit = 1d.FromMillimeters();

            var loop = new CurveLoop();

            //var edgeRef = RevitAPI.UIDocument.Selection.PickObject(ObjectType.Edge, "Выберите грани");
            //var line = RevitAPI.UIDocument.PickObject("Выбрать нижние линии") as ModelHermiteSpline;

            //using (Transaction t = new Transaction(doc, "test"))
            //{
            //    t.Start();

            //    var edge = edgeRef.ToElement().GetGeometryObjectFromReference(edgeRef) as Edge;
            //    var curve = edge.AsCurve();

            //    //var curveZPoints = curve.ControlPoints.Select(p => p.Z).ToList();

            //    //var lineCurve = line.GeometryCurve as HermiteSpline;

            //    //var points = lineCurve.ControlPoints;

            //    //var newPoints = new List<XYZ>();
            //    //for (var i = 0; i < points.Count; i++)
            //    //{
            //    //    var point = points[i];

            //    //    newPoints.Add(new XYZ(point.X, point.Y, curveZPoints[i]));
            //    //}

            //    //ExMethods.CreateDirectShape([HermiteSpline.Create(newPoints, curve.IsPeriodic)]);
            //    ExMethods.CreateDirectShape([curve]);


            //    t.Commit();
            //}


            var elems = RevitAPI.UIDocument.PickObjects("Выбрать нижние линии")
                .OrderBy(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString())
                .ToList();

            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                var curve1 = elems.First().get_Geometry(new Options()).FirstOrDefault() as Curve;
                var curve2 = elems.Last().get_Geometry(new Options()).FirstOrDefault() as HermiteSpline;

                var point1 = curve1.GetEndPoint(0);
                var point2 = curve2.GetEndPoint(1);

                var points = curve2.ControlPoints;
                points.RemoveAt(points.Count - 1);
                points.Add(point1);


                ExMethods.CreateDirectShape([Point.Create(point1)]);
                ExMethods.CreateDirectShape([Point.Create(point2)]);

                ExMethods.CreateDirectShape([HermiteSpline.Create(points, curve2.IsPeriodic)]);

                t.Commit();
            }

            //    try
            //    {
            //        loop.Append(curve);
            //        prefCurve = curve;

            //        //using (Transaction t = new Transaction(doc, "test"))
            //        //{
            //        //    t.Start();

            //        //    ExMethods.CreateDirectShape([curve]);

            //        //    t.Commit();
            //        //}
            //    }
            //    catch (Autodesk.Revit.Exceptions.ArgumentException)
            //    {
            //        var lastPoint = prefCurve.GetEndPoint(0);

            //        var newCtrlPoints = curve.ControlPoints;
            //        newCtrlPoints.RemoveAt(curve.ControlPoints.Count - 1);
            //        newCtrlPoints.Add(lastPoint);

            //        var newCurve = HermiteSpline.Create(newCtrlPoints, curve.IsPeriodic);

            //        using (Transaction t = new Transaction(doc, "test"))
            //        {
            //            t.Start();

            //            ExMethods.CreateDirectShape([Point.Create(lastPoint)]);
            //            ExMethods.CreateDirectShape([newCurve.CreateReversed()]);

            //            t.Commit();
            //        }

            //        //loop.Append(newCurve);
            //    }
            //}


            return Result.Succeeded;
        }
    }
}
#endif