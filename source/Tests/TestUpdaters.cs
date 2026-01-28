using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewUpdater : IUpdater
    {
        public void Execute(UpdaterData data)
        {
            var doc = data.GetDocument();

            foreach (var rebarId in data.GetAddedElementIds())
            {
                var rebar = rebarId.ToElement<FamilyInstance>(doc);
                
                var ownerViewId = rebar.OwnerViewId;
                if (ownerViewId == ElementId.InvalidElementId)
                    continue;

                var view = ownerViewId.ToElement<View>(doc);
                var idParamValue = view.get_Parameter(new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d")).AsString();
                if (!idParamValue.IsNullOrEmpty() && int.TryParse(idParamValue, out int id) && new ElementId(id).ToElement<FamilyInstance>() != null)
                {
                    rebar.get_Parameter(new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d")).Set(idParamValue);
                }
            }
        }

        public string GetAdditionalInformation() => "Передача Id палки из вида элементам 2д-армирования";
        public ChangePriority GetChangePriority() => ChangePriority.DetailComponents;
        public UpdaterId GetUpdaterId() => new UpdaterId(RevitAPI.Application.ActiveAddInId, new Guid("5E61380D-62D5-4E62-B64F-DB22EDE05FC4"));
        public string GetUpdaterName() => "2dRebarWallViewUpdater";
    }

    public class RebarWallUpdater : IUpdater
    {
        private Guid _idGuid = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");                    // OLP_Id
        private Guid _typeGuid = new Guid("215d6c56-3700-4db9-a5f5-53ec85b36daa");                  // OLP_Зона расположения
        private Guid _stepGuid = new Guid("5d7cb726-ac59-4f05-a902-8fdffa796d15");                  // ADSK_Шаг элементов
        private Guid _useScheduleGuid = new Guid("b220b6e8-254f-479f-95b8-62fc7123b098");           // OLP_Учет в спецификации

        private static bool _isExecuting = false;

        public void Execute(UpdaterData data)
        {
            var doc = data.GetDocument();

            var shParams = doc.ToElements<SharedParameterElement>();

            var idParamId = shParams.FirstOrDefault(p => p.GuidValue == _idGuid).Id;
            var typeParamId = shParams.FirstOrDefault(p => p.GuidValue == _typeGuid).Id;
            var useScheduleParamId = shParams.FirstOrDefault(p => p.GuidValue == _useScheduleGuid).Id;

            foreach (var rebarId in data.GetAddedElementIds())
                SetScheduleParameter(doc, rebarId.ToElement<FamilyInstance>(doc), idParamId, typeParamId);

            if (data.GetDeletedElementIds().Count != 0)
            {
                var rebars = doc.ToElements<FamilyInstance>(doc.ActiveView.Id, BuiltInCategory.OST_DetailComponents);
                foreach (var rebar in rebars)
                    SetScheduleParameter(doc, rebar, idParamId, typeParamId);
            }

            if (_isExecuting)
            {
                _isExecuting = false;
                return;
            }

            foreach (var rebarId in data.GetModifiedElementIds())
            {
                var rebar = rebarId.ToElement<FamilyInstance>(doc);

                var idParam = rebar.get_Parameter(_idGuid).AsString();
                var typeParam = rebar.get_Parameter(_typeGuid).AsString();

                if (idParam.IsNullOrEmpty() || typeParam.IsNullOrEmpty())
                    continue;

                typeParam = typeParam.Split('_')[0];

                var idParamFilter = idParamId.CreateEqualsFilter(idParam);
                var typeParamFilter = typeParamId.CreateEndsWithFilter(typeParam);

                if (data.IsChangeTriggered(rebarId, Element.GetChangeTypeParameter(typeParamId)))
                {
                    var sourceRebar = doc.ToElements(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter]))
                        .FirstOrDefault(r => r.Id.ToString() != rebarId.ToString());
                    if (sourceRebar == null)
                        return;

                    var sourceParams = sourceRebar
                        .GetOrderedParameters()
                        .Where(p => p.HasValue && (
                            (p.IsShared && !new[] { _useScheduleGuid, _stepGuid, _typeGuid }.Contains(p.GUID)) ||
                            p.Definition.ParameterGroup == BuiltInParameterGroup.PG_STRUCTURAL ||
                            p.Definition.ParameterGroup == BuiltInParameterGroup.PG_GEOMETRY
                        ));

                    foreach (var sourceParam in sourceParams)
                    {
                        var targetParam = sourceParam.IsShared || (sourceParam.Definition as InternalDefinition).BuiltInParameter != BuiltInParameter.INVALID
                            ? rebar.get_Parameter(sourceParam.Definition)
                            : rebar.LookupParameter(sourceParam.Definition.Name);

                        if (targetParam != null && !targetParam.IsReadOnly)
                        {
                            targetParam.SetValue(sourceParam.GetValue());
                        }
                    }

                    typeParam = rebar.get_Parameter((BuiltInParameter)typeParamId.IntegerValue).AsString();
                    typeParamFilter = typeParamId.CreateEqualsFilter(typeParam);

                    sourceRebar = doc.ToElements(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter]))
                        .FirstOrDefault(r => r.Id.ToString() != rebarId.ToString());

                    if (sourceRebar == null)
                        return;

                    rebar.get_Parameter(_stepGuid).SetValue(sourceRebar.get_Parameter(_stepGuid).GetValue());
                }
                else
                {
                    var palka = new ElementId(int.Parse(idParam)).ToElement(doc);
                    var palkaOffset = palka.LookupParameter("ГорАрм_ОтступОтТорца").AsDouble();
                    var palkaLength = palka.LookupParameter("Длина").AsDouble();

                    var sourceParameters = rebar
                        .GetOrderedParameters()
                        .Where(p => p.HasValue && (
                            (p.IsShared && !new[] { _useScheduleGuid, _stepGuid, _typeGuid }.Contains(p.GUID)) ||
                            p.Definition.ParameterGroup == BuiltInParameterGroup.PG_STRUCTURAL ||
                            p.Definition.ParameterGroup == BuiltInParameterGroup.PG_GEOMETRY
                        ));

                    var rebars = doc.ToElements(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter]));
                    foreach (var r in rebars)
                    {
                        foreach (var sourceParam in sourceParameters)
                        {
                            var targetParam = sourceParam.IsShared || (sourceParam.Definition as InternalDefinition).BuiltInParameter != BuiltInParameter.INVALID
                                ? r.get_Parameter(sourceParam.Definition)
                                : r.LookupParameter(sourceParam.Definition.Name);

                            if (targetParam != null && !targetParam.IsReadOnly)
                            {
                                targetParam.SetValue(sourceParam.GetValue());
                            }
                        }
                    }

                    typeParam = rebar.get_Parameter((BuiltInParameter)typeParamId.IntegerValue).AsString();
                    typeParamFilter = typeParamId.CreateEqualsFilter(typeParam);

                    foreach (var r in doc.ToElements(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter])))
                        r.get_Parameter(_stepGuid).SetValue(rebar.get_Parameter(_stepGuid).GetValue());

                    var pointRebars = rebars.Where(r => (r as FamilyInstance).Symbol.FamilyName.Contains("Точка"));
                    if (typeParam.Contains("ГорАрм") && rebar.get_Parameter(new Guid("844a01e2-19fc-4dc5-baa0-a4bda30ef1f6")).AsInteger() == 1)
                    {
                        var length = palkaLength - 2 * palkaOffset;
                        foreach (var pointRebar in pointRebars)
                        {
                            var lengthParam = pointRebar.get_Parameter(new Guid("d8841d49-a483-406a-b7c9-c8d3ceaf81b4"))        //OLP_Арм_L     
                                ?? pointRebar.get_Parameter(new Guid("b10d2260-5080-470d-be69-e136df3b45f6"));                  //OLP_Арм_Аdef

                            if (lengthParam != null && !lengthParam.IsReadOnly)
                                lengthParam.Set(length);
                        }
                    }
                    else
                    {
                        var lengthRebar = rebars.FirstOrDefault(r => (r as FamilyInstance).Symbol.FamilyName.Contains("Стержень"));
                        if (lengthRebar != null)
                        {
                            var lengthParam = lengthRebar.get_Parameter(new Guid("d8841d49-a483-406a-b7c9-c8d3ceaf81b4"))        //OLP_Арм_L     
                                ?? lengthRebar.get_Parameter(new Guid("b10d2260-5080-470d-be69-e136df3b45f6"));                  //OLP_Арм_Аdef
                            
                            if (lengthParam != null)
                            {
                                foreach (var pointRebar in pointRebars)
                                {
                                    var targetParam = pointRebar.get_Parameter(lengthParam.GUID);
                                    if (targetParam != null && !targetParam.IsReadOnly)
                                        targetParam.Set(lengthParam.AsDouble());
                                }
                            }
                        }
                    }

                    SetScheduleParameter(doc, rebar, idParamId, typeParamId);
                }
            }

            if (data.GetModifiedElementIds().Count != 0)
                _isExecuting = true;
        }

        private void SetScheduleParameter(Document doc, FamilyInstance rebar, ElementId idParamId, ElementId typeParamId)
        {
            var idParam = rebar.get_Parameter(_idGuid).AsString();
            var typeParam = rebar.get_Parameter(_typeGuid).AsString();

            if (idParam.IsNullOrEmpty() || typeParam.IsNullOrEmpty())
                return;

            typeParam = typeParam.Split('_')[0];

            var idParamFilter = idParamId.CreateEqualsFilter(idParam);
            var typeParamFilter = typeParamId.CreateEndsWithFilter(typeParam);

            var rebars = doc.ToElements(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter]));

            var elementsWithTrue = rebars
                .Where(element =>
                {
                    var param = element.get_Parameter(_useScheduleGuid) ?? element.LookupParameter("Учет в спецификации");
                    return param != null && param.AsInteger() == 1;
                })
                .ToList();

            if (typeParam == "ГорАрм" && rebar.get_Parameter(new Guid("844a01e2-19fc-4dc5-baa0-a4bda30ef1f6")).AsInteger() == 1)
            {
                if (elementsWithTrue.Count > 1)
                {
                    var pointRebar = elementsWithTrue.FirstOrDefault(r => (r as FamilyInstance).Symbol.FamilyName.Contains("Точка"));
                    if (pointRebar != null)
                        elementsWithTrue.Remove(pointRebar);

                    foreach (var element in elementsWithTrue)
                    {
                        var param = element.get_Parameter(_useScheduleGuid) ?? element.LookupParameter("Учет в спецификации");
                        if (!param.IsReadOnly)
                            param.Set(0);
                    }
                }

                if (elementsWithTrue.Count == 0)
                {
                    var r = rebars.FirstOrDefault(r => (r as FamilyInstance).Symbol.FamilyName.Contains("Точка"))
                        ?? rebars.First();

                    var p = r.get_Parameter(_useScheduleGuid) ?? r.LookupParameter("Учет в спецификации");
                    p.Set(1);
                }
            }
            else
            {
                if (elementsWithTrue.Count > 1)
                {
                    elementsWithTrue.Remove(elementsWithTrue[0]);
                    foreach (var element in elementsWithTrue)
                    {
                        var param = element.get_Parameter(_useScheduleGuid) ?? element.LookupParameter("Учет в спецификации");
                        if (!param.IsReadOnly)
                            param.Set(0);
                    }
                }

                if (elementsWithTrue.Count == 0)
                {
                    var r = rebars.FirstOrDefault(r => r.get_Parameter(_useScheduleGuid) != null
                                                   && !r.get_Parameter(_useScheduleGuid).IsReadOnly)
                        ?? rebars.First();

                    var p = r.get_Parameter(_useScheduleGuid) ?? r.LookupParameter("Учет в спецификации");
                    p.Set(1);
                }
            }
        }


        public string GetAdditionalInformation() => "Привязка элементов 2д-армирования стен";
        public ChangePriority GetChangePriority() => ChangePriority.DetailComponents;
        public UpdaterId GetUpdaterId() => new UpdaterId(RevitAPI.Application.ActiveAddInId, new Guid("F53272A9-D777-4E05-880C-AA514CB2766C"));
        public string GetUpdaterName() => "2dRebarWallUpdater";
    }
}