using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.Interfaces;
using BIMPlugins.ExtStorage.Methods;
using System.Linq;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateLoftGeometryCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;
            var intUnit = 1d.FromMillimeters();

            var bottomLoop = new CurveLoop();

            var elems = RevitAPI.UIDocument.PickObjects("Выбрать нижние линии")
                .OrderBy(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString())
                .ToList();
            
            foreach (var elem in elems)
            {
                var curve = elem.get_Geometry(new Options()).FirstOrDefault() as Curve;

                try
                {
                    bottomLoop.Append(curve);
                }
                catch
                {
                    bottomLoop.Append(curve.CreateReversed());
                }
            }

            var topLoop = new CurveLoop();

            var topElems = RevitAPI.UIDocument.PickObjects("Выбрать верхние линии")
                .OrderBy(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).AsString())
                .ToList();

            foreach (var elem in topElems)
            {
                var curve = elem.get_Geometry(new Options()).FirstOrDefault() as Curve;

                try
                {
                    topLoop.Append(curve);
                }
                catch
                {
                    topLoop.Append(curve.CreateReversed());
                }
            }

            Solid solid = GeometryCreationUtilities.CreateLoftGeometry(
                [bottomLoop, topLoop],
                new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId)
            );

            var famDoc = RevitAPI.Application.NewFamilyDocument(@"C:\ProgramData\Autodesk\RVT 2022\Family Templates\Russian\Метрическая система, типовая модель.rft");
            //var famDoc = RevitAPI.Application.Documents.Cast<Document>().First(d => d.Title == "Подрезка парапета.rfa");
            using (Transaction t = new Transaction(famDoc, "Создать FreeForm"))
            {
                t.Start();

                famDoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS).Set(1);
                famDoc.FamilyManager.NewType("Тип 1");

                var ffe = FreeFormElement.Create(famDoc, solid);
                ffe.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING).Set(1);

                t.Commit();
            }

            famDoc.PurgeUnused();

            using (TransactionGroup tGroup = new TransactionGroup(doc, "Загрузить семейство FreeForm"))
            {
                tGroup.Start();

                var fam = famDoc.LoadFamily(doc, new FamilyLoadOptions());

                using (Transaction t = new Transaction(doc, "Разместить семейство"))
                {
                    t.Start();

                    fam.Name = "Подрезка парапета пристроек";

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
    }
}
#endif