using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Linq;

namespace BIMPlugins.Test2dRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ViewUpdater : IUpdater
    {
        private Guid _idGuid = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");                    // OLP_Id

        public void Execute(UpdaterData data)
        {
            var doc = data.GetDocument();

            foreach (var viewId in data.GetModifiedElementIds())
            {
                var view = viewId.ToElement<View>(doc);
                
                var idParam = view.get_Parameter(_idGuid).AsString();
                foreach (var r in doc.ToElements<FamilyInstance>(view.Id, BuiltInCategory.OST_DetailComponents))
                    r.get_Parameter(_idGuid).Set(idParam);
            }

            foreach (var viewId in data.GetAddedElementIds())
            {
                var view = viewId.ToElement<View>(doc);

                var idParam = view.get_Parameter(_idGuid);
                if (idParam.HasValue)
                    idParam.Set(string.Empty);
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
        private Guid _pointLengthGuid = new Guid("b10d2260-5080-470d-be69-e136df3b45f6");           // OLP_Арм_Аdef

        public void Execute(UpdaterData data)
        {
            var doc = data.GetDocument();

            var shParams = doc.ToElements<SharedParameterElement>().ToList();

            var idParamId = shParams.FirstOrDefault(p => p.GuidValue == _idGuid).Id;
            var typeParamId = shParams.FirstOrDefault(p => p.GuidValue == _typeGuid).Id;
            var useScheduleParamId = shParams.FirstOrDefault(p => p.GuidValue == _useScheduleGuid).Id;

            foreach (var rebarId in data.GetAddedElementIds())
            {
                var rebar = rebarId.ToElement<FamilyInstance>(doc);

                var ownerViewId = rebar.OwnerViewId;
                if (ownerViewId == ElementId.InvalidElementId)
                    continue;

                var view = ownerViewId.ToElement<View>(doc);
                var idParamValue = view.get_Parameter(_idGuid).AsString();
                if (!idParamValue.IsNullOrEmpty())
                {
                    rebar.get_Parameter(_idGuid).Set(idParamValue);
                }

                SetScheduleParameter(doc, rebar, idParamId, typeParamId);
            }   

            if (data.GetDeletedElementIds().Count != 0)
            {
                var rebars = doc.ToElements<FamilyInstance>(doc.ActiveView.Id, BuiltInCategory.OST_DetailComponents);
                foreach (var rebar in rebars)
                    SetScheduleParameter(doc, rebar, idParamId, typeParamId);

                var groupedRebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents)
                    .Where(r => !r.get_Parameter(_idGuid).AsString().IsNullOrEmpty())
                    .GroupBy(r => r.get_Parameter(_idGuid).AsString())
                    .Select(g => g.First());

                foreach (var rebar in groupedRebars)
                {
                    var param = rebar.get_Parameter(_idGuid);

                    var value = param.AsString().Split(';');
                    var result = string.Join(";",
                        value.Where(id =>
                            new ElementId(int.Parse(id)).ToElement(doc) != null
                        )
                    );

                    foreach (var view in doc.ToElements<View>(idParamId.CreateEqualsFilter(param.AsString())))
                        view.get_Parameter(_idGuid).Set(result);
                }
            }

            foreach (var rebarId in data.GetModifiedElementIds())
            {
                var rebar = rebarId.ToElement<FamilyInstance>(doc);

                var idParam = rebar.get_Parameter(_idGuid).AsString();
                var typeParam = rebar.get_Parameter(_typeGuid).AsString();

                if (idParam.IsNullOrEmpty() || typeParam.IsNullOrEmpty())
                    continue;

                var splitTypeParam = typeParam.Split('_')[0];

                var idParamFilter = idParamId.CreateEqualsFilter(idParam);
                var typeParamFilter = splitTypeParam.Contains("ВертАрм") ? typeParamId.CreateEqualsFilter(typeParam) : typeParamId.CreateContainsFilter(splitTypeParam);
                var andFilter = new LogicalAndFilter([idParamFilter, typeParamFilter]);
                
                var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, andFilter).ToList();

                if (data.IsChangeTriggered(rebarId, Element.GetChangeTypeParameter(typeParamId)))
                {
                    var sourceRebar = rebars.FirstOrDefault(r => r.Id.ToString() != rebarId.ToString());
                    if (sourceRebar == null)
                        return;

                    var sourceParams = sourceRebar
                        .GetOrderedParameters()
                        .Where(p => p.HasValue && (
                            p.IsShared && !new[] { _useScheduleGuid, _stepGuid, _typeGuid }.Contains(p.GUID) ||
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

                    if (splitTypeParam == "ГорАрм" || splitTypeParam == "Шпилька")
                    {
                        typeParamFilter = typeParamId.CreateEqualsFilter(typeParam);

                        sourceRebar = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter]))
                            .FirstOrDefault(r => r.Id.ToString() != rebarId.ToString());

                        if (sourceRebar == null)
                            return;
                    }
                    else if (splitTypeParam.Contains("ВертАрм"))
                    {
                        typeParamFilter = typeParamId.CreateEqualsFilter(splitTypeParam);

                        sourceRebar = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter]))
                            .FirstOrDefault(r => r.Id.ToString() != rebarId.ToString());

                        if (sourceRebar == null)
                            return;
                    }

                    rebar.get_Parameter(_stepGuid).SetValue(sourceRebar.get_Parameter(_stepGuid).GetValue());
                }
                else
                {
                    var sourceParameters = rebar
                        .GetOrderedParameters()
                        .Where(p => p.HasValue && (
                            p.IsShared && !new[] { _useScheduleGuid, _stepGuid, _typeGuid, _pointLengthGuid }.Contains(p.GUID) ||
                            !p.IsShared && new[] { BuiltInParameterGroup.PG_GEOMETRY, BuiltInParameterGroup.PG_STRUCTURAL }.Contains(p.Definition.ParameterGroup)
                        ));

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

                    if (splitTypeParam == "ГорАрм" || splitTypeParam == "Шпилька")
                    {
                        typeParamFilter = typeParamId.CreateEqualsFilter(typeParam);

                        foreach (var r in doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter])))
                            r.get_Parameter(_stepGuid).SetValue(rebar.get_Parameter(_stepGuid).GetValue());
                    }
                    else if (splitTypeParam == "ВертАрм")
                    {
                        typeParamFilter = typeParamId.CreateContainsFilter(splitTypeParam);
                        var typeParamNotContainsFilter = typeParamId.CreateNotContainsFilter("ВертАрмТорца");

                        foreach (var r in doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter, typeParamNotContainsFilter])))
                            r.get_Parameter(_stepGuid).SetValue(rebar.get_Parameter(_stepGuid).GetValue());
                    }
                    else if (splitTypeParam == "ВертАрмТорца")
                    {
                        typeParamFilter = typeParamId.CreateContainsFilter(splitTypeParam);

                        foreach (var r in doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter])))
                            r.get_Parameter(_stepGuid).SetValue(rebar.get_Parameter(_stepGuid).GetValue());
                    }
                    else
                        foreach (var r in rebars)
                            r.get_Parameter(_stepGuid).SetValue(rebar.get_Parameter(_stepGuid).GetValue());

                    SetLengthToPointRebars(doc, rebar, idParamId, typeParamId);
                    SetScheduleParameter(doc, rebar, idParamId, typeParamId);
                }
            }
        }

        private void SetLengthToPointRebars(Document doc, FamilyInstance rebar, ElementId idParamId, ElementId typeParamId)
        {
            var idParam = rebar.get_Parameter(_idGuid).AsString();
            var typeParam = rebar.get_Parameter(_typeGuid).AsString();

            var splitTypeParam = typeParam.Split('_')[0];

            var idParamFilter = idParamId.CreateEqualsFilter(idParam);
            var typeParamFilter = splitTypeParam.Contains("ВертАрм")
                ? typeParamId.CreateContainsFilter("ВертАрм")
                : typeParamId.CreateContainsFilter(splitTypeParam);

            var row2Filter = typeParam.Contains("Доп")
                ? typeParamId.CreateContainsFilter("Доп")
                : typeParamId.CreateNotContainsFilter("Доп");

            var rebarFormParam = rebar.Symbol.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d"));
            rebarFormParam ??= rebar.GetSubComponentIds()
                .FirstOrDefault()?
                .ToElement<FamilyInstance>()
                .Symbol.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d"));

            if (rebarFormParam == null)
                return;

            var rebarForm = rebarFormParam.AsInteger();

            var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, new LogicalAndFilter([idParamFilter, typeParamFilter, row2Filter]));
            var pointRebars = rebars.Where(r =>
            {
                var rebarFormParam = r.Symbol.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d"));
                rebarFormParam ??= r.GetSubComponentIds().FirstOrDefault()?.ToElement<FamilyInstance>().Symbol.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d"));
                if (rebarFormParam == null)
                    return false;

                return r.Symbol.FamilyName.Contains("Точка") && rebarFormParam.AsInteger() == rebarForm;
            });

            if (splitTypeParam.Contains("ГорАрм") && rebar.get_Parameter(new Guid("844a01e2-19fc-4dc5-baa0-a4bda30ef1f6")).AsInteger() == 1)
            {
                double length = 0;
                foreach (var id in idParam.Split(';'))
                {
                    var palka = new ElementId(int.Parse(id)).ToElement(doc);
                    var palkaOffset = palka.LookupParameter("ГорАрм_ОтступОтТорца").AsDouble();
                    var palkaLength = palka.LookupParameter("Длина").AsDouble();

                    length += palkaLength - 2 * palkaOffset;
                }

                foreach (var pointRebar in pointRebars)
                {
                    var lengthParam = pointRebar.get_Parameter(_pointLengthGuid);

                    if (lengthParam != null && !lengthParam.IsReadOnly && lengthParam.AsDouble().Round() != length.Round())
                        lengthParam.Set(length);
                }
            }
            else
            {
                var lengthRebar = rebars.FirstOrDefault(r => r.Symbol.FamilyName.Contains("Стержень") && r.Symbol.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d")) != null &&
                                                             r.Symbol.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d")).AsInteger() == rebarForm);
                if (lengthRebar != null)
                {
                    var lengthParam = lengthRebar.get_Parameter(_pointLengthGuid);

                    if (lengthParam != null)
                    {
                        foreach (var pointRebar in pointRebars)
                        {
                            var targetParam = pointRebar.get_Parameter(lengthParam.GUID);
                            if (targetParam != null && !targetParam.IsReadOnly && targetParam.AsDouble().Round() != lengthParam.AsDouble().Round())
                                targetParam.Set(lengthParam.AsDouble());
                        }
                    }
                }
            }
        }
        private void SetScheduleParameter(Document doc, FamilyInstance rebar, ElementId idParamId, ElementId typeParamId)
        {
            var idParam = rebar.get_Parameter(_idGuid).AsString();
            var typeParam = rebar.get_Parameter(_typeGuid).AsString();

            if (idParam.IsNullOrEmpty() || typeParam.IsNullOrEmpty())
                return;

            var splitTypeParam = typeParam.Split('_')[0];
            
            var idParamFilter = idParamId.CreateEqualsFilter(idParam);
            var typeParamFilter = splitTypeParam.Contains("ВертАрм") ? typeParamId.CreateEqualsFilter(typeParam) : typeParamId.CreateContainsFilter(splitTypeParam);
            var andFilter = new LogicalAndFilter([idParamFilter, typeParamFilter]);

            var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, andFilter);

            var elementsWithTrue = rebars
                .Where(element =>
                {
                    var param = element.get_Parameter(_useScheduleGuid) ?? element.LookupParameter("Учет в спецификации");
                    return param != null && param.AsInteger() == 1;
                })
                .ToList();

            if (splitTypeParam == "ГорАрм" && rebar.get_Parameter(new Guid("844a01e2-19fc-4dc5-baa0-a4bda30ef1f6")).AsInteger() == 1)
            {
                if (elementsWithTrue.Count > 1)
                {
                    var pointRebar = elementsWithTrue.FirstOrDefault(r => r.Symbol.FamilyName.Contains("Точка"));
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
                    var r = rebars.FirstOrDefault(r => r.Symbol.FamilyName.Contains("Точка"))
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