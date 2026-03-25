using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Test2dRebar.Classes;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Test2dRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RegisterUpdatersCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var familyParamId = new ElementId(BuiltInParameter.ELEM_FAMILY_PARAM);
            var idParameterId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == RebarMethods.IdGuid).Id;
            var typeParameterId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == RebarMethods.TypeGuid).Id;
            var numberParameterId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == RebarMethods.NumberGuid).Id;

            UnRegisterUpdaters(new ViewUpdater(), doc);
            UnRegisterUpdaters(new RebarWallUpdater(), doc);
            UnRegisterUpdaters(new PalkaUpdater(), doc);

            RegisterUpdater(
                new ViewUpdater(),
                doc,
                new ElementMulticlassFilter(
                [
                    typeof(ViewPlan),
                    typeof(ViewSection)
                ]),
                [
                    Element.GetChangeTypeParameter(idParameterId),
                    Element.GetChangeTypeElementAddition(),
                    Element.GetChangeTypeElementDeletion()
                ]
            );
            RegisterUpdater(
                new RebarWallUpdater(),
                doc,
                new LogicalAndFilter(
                [
                    new ElementCategoryFilter(BuiltInCategory.OST_DetailComponents),
                    new ElementClassFilter(typeof(FamilyInstance)),
                    new LogicalOrFilter([familyParamId.CreateBeginsWithFilter("280"), familyParamId.CreateBeginsWithFilter("281")])
                ]),
                [
                    Element.GetChangeTypeParameter(idParameterId),
                    Element.GetChangeTypeParameter(typeParameterId),
                    Element.GetChangeTypeParameter(new ElementId(BuiltInParameter.ELEM_TYPE_PARAM)),
                    Element.GetChangeTypeElementAddition(),
                    Element.GetChangeTypeElementDeletion(),
                    Element.GetChangeTypeAny()
                ]
            );
            RegisterUpdater(
                new PalkaUpdater(),
                doc,
                new LogicalAndFilter(
                [
                    new ElementCategoryFilter(BuiltInCategory.OST_DetailComponents),
                    new ElementClassFilter(typeof(FamilyInstance)),
                    familyParamId.CreateBeginsWithFilter("285")
                ]),
                [
                    Element.GetChangeTypeParameter(idParameterId),
                    Element.GetChangeTypeParameter(numberParameterId),
                    Element.GetChangeTypeElementDeletion(),
                    Element.GetChangeTypeAny()
                ]
            );

            return Result.Succeeded;
        }

        private static void RegisterUpdater(IUpdater updater, Document document, ElementFilter elementFilter, List<ChangeType> changeTypes)
        {
            var updaterId = updater.GetUpdaterId();

            if (UpdaterRegistry.IsUpdaterRegistered(updaterId, document))
            {
                return;
            }

            UpdaterRegistry.RegisterUpdater(updater, document, true);

            foreach (var changeType in changeTypes)
            {
                UpdaterRegistry.AddTrigger(updaterId, document, elementFilter, changeType);
            }
        }
        private static void UnRegisterUpdaters(IUpdater updater, Document document)
        {
            var updaterId = updater.GetUpdaterId();

            if (UpdaterRegistry.IsUpdaterRegistered(updaterId, document))
            {
                UpdaterRegistry.UnregisterUpdater(updaterId, document);
            }
        }


        private void SetLength(FamilyInstance familyInstance, Parameter parameter)
        {
            var rebLine = (familyInstance.Location as LocationCurve).Curve as Line;

            var direction = rebLine.Direction;
            direction = direction.Normalize();

            XYZ startPoint;
            if (rebLine.Direction.Z.Round() == 1)
            {
                startPoint = rebLine.GetEndPoint(0);
            }
            else
            {
                startPoint = rebLine.GetEndPoint(1);
                direction = direction.Negate();
            }

            XYZ endPoint = startPoint + direction * parameter.AsDouble();

            (familyInstance.Location as LocationCurve).Curve = Line.CreateBound(startPoint, endPoint);
        }
    }
}