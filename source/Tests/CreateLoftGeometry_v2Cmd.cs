using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Interfaces;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Linq;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateLoftGeometry_v2Cmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;
            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            var lineRefs = RevitAPI.UIDocument.Selection.PickObjects(ObjectType.Edge, "Выберите грани");

            var famDoc = RevitAPI.Application.NewFamilyDocument(@"C:\ProgramData\Autodesk\RVT 2022\Family Templates\Russian\Метрическая система, типовая модель.rft");
            //var famDoc = RevitAPI.Application.Documents.Cast<Document>().First(d => d.Title == "Подрезка парапета.rfa");
            using (Transaction t = new Transaction(famDoc, "Создать FreeForm"))
            {
                t.Start();

                famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS).Set(1);
                famDoc.FamilyManager.NewType("Тип 1");

                foreach (var lineRef in lineRefs)
                {
                    var edge = lineRef.ToElement().GetGeometryObjectFromReference(lineRef) as Edge;

                    var solid = CreateVerticalCutter(edge.AsCurve(), 3000 * intUnit, 10000 * intUnit, 300 * intUnit);
                    var ffe = FreeFormElement.Create(famDoc, solid);
                    ffe.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING).Set(1);
                }

                t.Commit();
            }

            famDoc.PurgeUnused();

            using (TransactionGroup tGroup = new TransactionGroup(doc, "Разместить семейство FreeForm"))
            {
                tGroup.Start();

                var fam = famDoc.LoadFamily(doc, new FamilyLoadOptions());

                using (Transaction t = new Transaction(doc, "Разместить семейство"))
                {
                    t.Start();

                    fam.Name = "Подрезка парапета";

                    var symbol = fam.GetFamilySymbolIds().First().ToElement<FamilySymbol>();
                    if (!symbol.IsActive)
                        symbol.Activate();

                    doc.Create.NewFamilyInstance(XYZ.Zero, symbol, StructuralType.NonStructural);

                    t.Commit();
                }

                tGroup.Assimilate();
            }

            famDoc.Close(false);

            return Result.Succeeded;
        }

        private static Solid CreateVerticalCutter(Curve path, double width, double height, double maxStep)
        {
            var profileCount = path is Line
                ? 2
                : Math.Max(2, (int)Math.Ceiling(path.Length / maxStep) + 1);

            var profiles = new List<CurveLoop>(profileCount);

            for (int i = 0; i < profileCount; i++)
            {
                var parameter = (double)i / (profileCount - 1);

                var derivatives = path.ComputeDerivatives(
                    parameter,
                    normalized: true);

                var horizontalTangent = new XYZ(
                    derivatives.BasisX.X,
                    derivatives.BasisX.Y,
                    0);

                var side = XYZ.BasisZ
                    .CrossProduct(horizontalTangent)
                    .Normalize();

                profiles.Add(CreateVerticalProfile(
                    derivatives.Origin,
                    side,
                    width,
                    height));
            }

            return GeometryCreationUtilities.CreateLoftGeometry(
                profiles,
                new SolidOptions(
                    ElementId.InvalidElementId,
                    ElementId.InvalidElementId));
        }
        private static CurveLoop CreateVerticalProfile(XYZ center, XYZ side, double width, double height)
        {
            var halfOffset = side * (width / 2);
            var vertical = XYZ.BasisZ * height;

            var p0 = center - halfOffset;
            var p1 = center + halfOffset;
            var p2 = p1 + vertical;
            var p3 = p0 + vertical;

            return CurveLoop.Create(
            [
                Line.CreateBound(p0, p1),
            Line.CreateBound(p1, p2),
            Line.CreateBound(p2, p3),
            Line.CreateBound(p3, p0)
            ]);
        }
    }
}
#endif