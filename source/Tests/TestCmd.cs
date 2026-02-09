using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using BIMPlugins.Test2dRebar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Xml.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            //Guid _idGuid = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");                    // OLP_Id
            //Guid _typeGuid = new Guid("215d6c56-3700-4db9-a5f5-53ec85b36daa");                  // OLP_Зона расположения
            //Guid _stepGuid = new Guid("5d7cb726-ac59-4f05-a902-8fdffa796d15");                  // ADSK_Шаг элементов
            //Guid _useScheduleGuid = new Guid("b220b6e8-254f-479f-95b8-62fc7123b098");           // OLP_Учет в спецификации

            //var shParams = doc.ToElements<SharedParameterElement>();

            //var idParamId = shParams.FirstOrDefault(p => p.GuidValue == _idGuid).Id;
            //var typeParamId = shParams.FirstOrDefault(p => p.GuidValue == _typeGuid).Id;
            //var useScheduleParamId = shParams.FirstOrDefault(p => p.GuidValue == _useScheduleGuid).Id;

            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                //var rebar = new ElementId(25759661).ToElement();

                //var idParam = rebar.get_Parameter(_idGuid).AsString();
                ////var typeParam = rebar.get_Parameter(_typeGuid).AsString();

                //////typeParam = typeParam.Split('_')[0];

                ////var idParamFilter = idParamId.CreateEqualsFilter("25749139");
                //////var typeParamFilter = typeParamId.CreateContainsFilter(typeParam);

                ////var rebars = doc.ToElements(idParamFilter);
                ////foreach (var r in rebars)
                ////{
                ////    r.get_Parameter(_idGuid).Set("25782650");
                ////}

                //double length = 0;
                //foreach (var id in idParam.Split(';'))
                //{
                //    var palka = new ElementId(int.Parse(id)).ToElement(doc);
                //    var palkaOffset = palka.LookupParameter("ГорАрм_ОтступОтТорца").AsDouble();
                //    var palkaLength = palka.LookupParameter("Длина").AsDouble();

                //    length += palkaLength - 2 * palkaOffset;
                //}

                //var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents)
                //    .Where(r => r.Symbol.FamilyName == "E-SHP-70 - Дуга_x021");

                //var newSymbol = new ElementId(31820257).ToElement<FamilySymbol>();

                //foreach (var rebar in rebars)
                //{
                //    var p1 = rebar.LookupParameter("Прямой участок1").AsDouble();
                //    if (p1 == 0)
                //        continue;

                //    var p2 = rebar.LookupParameter("Радиус").AsDouble();
                //    var p3 = rebar.LookupParameter("Угол").AsDouble();

                //    rebar.Symbol = newSymbol;

                //    rebar.LookupParameter("Прямой участок1").Set(p1);
                //    rebar.LookupParameter("Радиус").Set(p2);
                //    rebar.LookupParameter("Угол").Set(p3);
                //}

                t.Commit();
            }

            var typeParameter = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == new Guid("215d6c56-3700-4db9-a5f5-53ec85b36daa"));
            var idParameter = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d"));

            UnRegisterUpdaters(new RebarWallUpdater(), doc);
            UnRegisterUpdaters(new ViewUpdater(), doc);  
            
            RegisterUpdater(
                new ViewUpdater(),
                doc,
                new LogicalOrFilter([new ElementClassFilter(typeof(ViewPlan)), new ElementClassFilter(typeof(ViewSection))]),
                [Element.GetChangeTypeParameter(idParameter.Id)]
            );
            RegisterUpdater(
                new RebarWallUpdater(),
                doc,
                new LogicalAndFilter([new ElementClassFilter(typeof(FamilyInstance)), new ElementCategoryFilter(BuiltInCategory.OST_DetailComponents)]),
                [
                    Element.GetChangeTypeParameter(typeParameter.Id),
                    Element.GetChangeTypeElementAddition(),
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