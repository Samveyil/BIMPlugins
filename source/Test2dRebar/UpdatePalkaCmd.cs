using Aspose.Cells.Charts;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using BIMPlugins.Test2dRebar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Test2dRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdatePalkaCmd : IExternalCommand
    {
        private Guid _idGuid = RebarMethods.IdGuid;
        private Guid _typeGuid = RebarMethods.TypeGuid;
        private Guid _stepGuid = RebarMethods.StepGuid;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var palkaTypeDic = new Dictionary<string, Guid>()
            {
                {"_Количество",  new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a")},
                {"_Диаметр", new Guid("a13859b3-a733-4df0-ba54-ba74966408e9") },
                {"_Шаг", new Guid("5d7cb726-ac59-4f05-a902-8fdffa796d15")}
            };

            var palkaParamGuids = new List<Guid>
            {
                new Guid("e1b06433-f527-403c-8986-af9a01e6be7f"),           // ADSK_Комплект чертежей
                new Guid("92ae0425-031b-40a9-8904-023f7389963b"),           // ADSK_Марка изделия
                /*new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"), */          // ADSK_Марка конструкции
                new Guid("b5aee52e-5294-46e8-8086-f76421185a84"),           // OLP_Количество конструкций
                /*new Guid("0134e43b-3fd9-40bb-9abd-41fa4f5b6481"),*/           // OLP_Количество сборок
                new Guid("5776fb34-04bb-4a41-8f43-81edd9b0daff"),           // OLP_Специфицировать сборку
                new Guid("f59036bb-fa3d-40a8-bcf6-f6bbc54f26b6")            // OLP_Этаж
            };

            var idParamId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == _idGuid).Id;

            View view;
            var selectedPalka = RevitAPI.UIDocument.ToSelectedElements().FirstOrDefault(e => e is FamilyInstance instance && instance.Symbol.FamilyName.Contains("Палка"));
            if (selectedPalka != null)
            {
                var viewId = selectedPalka.get_Parameter(_idGuid).AsString()?.Split(';')[0];
                if (viewId.IsNullOrEmpty())
                {
                    message = "Не возможно определить вид, относящийся к выбранной палке"!;
                    return Result.Cancelled;
                }

                view = new ElementId(int.Parse(viewId)).ToElement<View>();
            }
            else
                view = doc.ActiveView;

            var ids = view.get_Parameter(_idGuid).AsString();
            if (ids.IsNullOrEmpty())
            {
                message = "Не возможно определить палки, относящиеся к активному виду"!;
                return Result.Cancelled;
            }

            var idParamFilter = idParamId.CreateEqualsFilter(ids);

            var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, idParamFilter)
                .Where(r => !r.get_Parameter(_typeGuid).AsString().IsNullOrEmpty())
                .ToList();

            XYZ edgeRebarUpViewDirect = null;
            List<IGrouping<RebarGroupKey, FamilyInstance>> sectionGroups = [];

            var edgeRebar = rebars.FirstOrDefault(r => r.get_Parameter(_typeGuid).AsString().Contains("ВертАрмТорца") || r.get_Parameter(_typeGuid).AsString() == "ГорПка");
            if (edgeRebar != null)
            {
                var edgeRebarView = edgeRebar.OwnerViewId.ToElement<View>();
                edgeRebarUpViewDirect = edgeRebarView.UpDirection.Normalize();

                var wall = doc.ToElements<Wall>(edgeRebarView.Id)
                    .FirstOrDefault(w => (w.Location as LocationCurve).Curve is Line line &&
                        (line.Direction.Normalize().IsAlmostEqualTo(edgeRebarUpViewDirect) || line.Direction.Normalize().IsAlmostEqualTo(edgeRebarUpViewDirect.Negate())));

                var wallMidlPoint = (wall.Location as LocationCurve).Curve.Evaluate(0.5, true);

                sectionGroups = rebars
                    .Where(r => r.get_Parameter(_typeGuid).AsString().Contains("ВертАрмТорца") || r.get_Parameter(_typeGuid).AsString() == "ГорПка")
                    .GroupBy(r => new RebarGroupKey(r.get_Parameter(_typeGuid).AsString(), r.IsAboveCenter(wallMidlPoint, edgeRebarUpViewDirect)))
                    .ToList();
            }

            var typeRebars = rebars.GroupBy(r => r.get_Parameter(_typeGuid).AsString())
                .Select(g => g.First())
                .ToList();

            var types = typeRebars.Select(r => r.get_Parameter(_typeGuid).AsString());
            var hasTopRebar = types.Any(t => t == "ГорАрм_ДопШагСверху");
            var hasBottomRebar = types.Any(t => t == "ГорАрм_ДопШагСнизу");

            var countDict = new Dictionary<string, double>();

            FamilyInstance palka = null;
            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                foreach (var id in ids.Split(';'))
                {
                    palka = new ElementId(int.Parse(id)).ToElement<FamilyInstance>();

                    foreach (var visibleParam in palka.Parameters.Cast<Parameter>().Where(p => p.Definition.Name.EndsWith(".Вкл") && !p.IsReadOnly))
                        visibleParam.Set(0);

                    foreach (var diamParam in palka.Parameters.Cast<Parameter>().Where(p => p.Definition.Name.EndsWith("_Диаметр") && !p.IsReadOnly))
                        diamParam.Set(0);

                    doc.Regenerate();

                    palka.LookupParameter("ГорАрм_ДопШагСверху.Вкл").Set(hasTopRebar ? 1 : 0);
                    palka.LookupParameter("ГорАрм_ДопШагСнизу.Вкл").Set(hasBottomRebar ? 1 : 0);

                    foreach (var sectionGroup in sectionGroups)
                    {
                        var palkaDirect = ((palka.Location as LocationCurve).Curve as Line).Direction.Normalize();
                        var inverted = palkaDirect.IsAlmostEqualTo(edgeRebarUpViewDirect.Negate());

                        var location = sectionGroup.Key.IsAbove
                            ? inverted ? "Начало" : "Конец"
                            : inverted ? "Конец" : "Начало";

                        var visParam = palka.LookupParameter($"{sectionGroup.Key.Type}_{location}.Вкл");
                        visParam?.Set(1);
                    }

                    foreach (var typeRebar in typeRebars)
                    {
                        var rType = typeRebar.get_Parameter(_typeGuid).AsString();

                        var visParam = palka.LookupParameter($"{rType}.Вкл");
                        visParam?.Set(1);

                        if (rType == "ГорАрм_ДопШагСнизу" || rType == "ГорАрм_ДопШагСверху")
                        {
                            palka.LookupParameter(rType).Set(typeRebar.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble());

                            var countArray = typeRebar.LookupParameter("Колво.Расч");
                            if (countArray == null)
                                continue;

                            if (rType == "ГорАрм_ДопШагСнизу")
                            {
                                palka.LookupParameter($"OLP_ГорАрм_Нижняя зона учащения")
                                    .Set(typeRebar.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble() * countArray.AsInteger());
                            }
                            else
                            {
                                palka.LookupParameter($"OLP_ГорАрм_Верхняя зона учащения")
                                    .Set(typeRebar.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble() * countArray.AsInteger());
                            }
                        }

                        if (!rType.Contains("ГорАрм") && (!rType.Contains("ВертАрм") || rType.Contains("ВертАрмТорца")))
                            rType = rType.Split('_')[0];

                        if (palka.Symbol.FamilyName.Contains("Пилон") && rType == "ГорАрм")
                        {
                            if (typeRebar.GetSymbolParameter(RebarMethods.PrefixGuid).AsString() == "Х")
                                palka.LookupParameter("Хомут.Вкл").Set(1);
                        }

                        foreach (var kvp in palkaTypeDic.Skip(1))
                        {
                            var palkaParam = palka.Parameters.Cast<Parameter>()
                                .FirstOrDefault(p => p.Definition.Name == $"{rType}{kvp.Key}");
                            if (palkaParam == null)
                                continue;

                            palkaParam.SetValue(typeRebar.get_Parameter(kvp.Value).GetValue());
                        }
                    }

                    doc.Regenerate();

                    foreach (var group in typeRebars.GroupBy(r => r.get_Parameter(_typeGuid).AsString().Split('_')[0]))
                    {
                        var rType = group.Key;

                        var palkaParam = palka.Parameters.Cast<Parameter>()
                            .FirstOrDefault(p => p.Definition.Name == $"{rType}_Количество");
                        if (palkaParam == null)
                            continue;

                        if (!countDict.ContainsKey(rType))
                            countDict[rType] = 0;

                        if (rType == "ГорАрм" && group.First().get_Parameter(new Guid("844a01e2-19fc-4dc5-baa0-a4bda30ef1f6")).AsInteger() == 1)
                            countDict[rType] = palkaParam.AsDouble();
                        else
                            countDict[rType] += palkaParam.AsDouble();
                    }
                }
                
                /// Корректировка кол-ва ВертАрм
                var dopRebars = typeRebars
                    .Where(r => r.get_Parameter(_typeGuid).AsString().Contains("ВертАрм_Доп"))
                    .ToList();

                if (dopRebars.Count != 0)
                {
                    if (!countDict.ContainsKey("ВертАрм_Доп"))
                        countDict["ВертАрм_Доп"] = 0;

                    if (dopRebars.FirstOrDefault(r => r.get_Parameter(_typeGuid).AsString() == "ВертАрм_Доп_2ряд") != null)
                    {
                        countDict["ВертАрм_Доп"] += (countDict["ВертАрм"] / 2).Round(0);
                    }
                    else if (rebars.Where(r => r.get_Parameter(_typeGuid).AsString().Contains("ВертАрм_Доп")).GroupBy(r => r.OwnerViewId).FirstOrDefault(g => g.Count() == 1) != null)
                    {
                        countDict["ВертАрм_Доп"] += (countDict["ВертАрм"] / 2).Round(0);
                    }
                    else
                    {
                        palka.LookupParameter("ВертАрм_Доп_2ряд_Диаметр").Set(palka.LookupParameter("ВертАрм_Доп_Диаметр").AsDouble());
                        
                        countDict["ВертАрм_Доп"] += countDict["ВертАрм"];
                    }
                }

                if (typeRebars.FirstOrDefault(r => r.get_Parameter(_typeGuid).AsString() == "ВертАрм_2ряд") != null)
                    countDict["ВертАрм"] = (countDict["ВертАрм"] / 2).Round(0);

                /// Корректировка кол-ва ВертАрмТорца
                //var endDopRebars = typeRebars
                //    .Where(r => r.get_Parameter(_typeGuid).AsString().Contains("ВертАрмТорца_Доп"))
                //    .ToList();

                //if (endDopRebars.Count != 0)
                //{
                //    if (!countDict.ContainsKey("ВертАрмТорца_Доп"))
                //        countDict["ВертАрмТорца_Доп"] = 0;

                //    countDict["ВертАрмТорца_Доп"] += rebars.Where(r => r.get_Parameter(_typeGuid).AsString().Contains("ВертАрмТорца_Доп")).Count();

                //    palka.LookupParameter("ВертАрмТорца_Доп_Диаметр").Set(endDopRebars.First().get_Parameter(palkaTypeDic["_Диаметр"]).AsDouble());
                //}

                //if (typeRebars.FirstOrDefault(r => r.get_Parameter(_typeGuid).AsString() == "ВертАрмТорца_2ряд") != null)
                //    countDict["ВертАрмТорца"] = (countDict["ВертАрмТорца"] / 2).Round(0);
                
                //if (typeRebars.FirstOrDefault(r => r.get_Parameter(_typeGuid).AsString() == "ВертАрмТорца_Доп") != null &&
                //    typeRebars.FirstOrDefault(r => r.get_Parameter(_typeGuid).AsString() == "ВертАрмТорца_Доп_2ряд") != null)
                //    countDict["ВертАрмТорца_Доп"] = (countDict["ВертАрмТорца_Доп"] / 2).Round(0);

                foreach (var sectionGroup in sectionGroups.Where(g => g.Key.Type != "ГорПка"))
                {
                    countDict[sectionGroup.Key.Type] = sectionGroup.Count();
                }

                /// Корректировка кол-ва ВертПка
                if (rebars.Where(r => r.get_Parameter(_typeGuid).AsString() == "ВертПка").Count() > 1)
                    countDict["ВертПка"] = countDict["ВертПка"] * 2;

                foreach (var r in rebars)
                {
                    var rType = r.get_Parameter(_typeGuid).AsString();

                    if (rType.Contains("ВертАрм_Доп"))
                        rType = "ВертАрм_Доп";
                    else
                        rType = rType.Split('_')[0];

                    if (!countDict.ContainsKey(rType))
                        continue;

                    var targetParam = r.get_Parameter(palkaTypeDic["_Количество"]);
                    if (targetParam == null || targetParam.IsReadOnly)
                    {
                        if (r.LookupParameter("Количество_Вручную") != null)
                            r.LookupParameter("Количество_Вручную").Set(countDict[rType]);
                        else
                        {
                            r.LookupParameter("Задать количество").Set(1);
                            r.LookupParameter("Количество").Set(countDict[rType]);
                        }
                    }
                    else if (!targetParam.IsReadOnly)
                        targetParam.Set(countDict[rType]);

                    if (rType.Contains("ВертАрмТорца"))
                        r.get_Parameter(new Guid("0134e43b-3fd9-40bb-9abd-41fa4f5b6481")).Set(ids.Split(';').Count());

                    var palkaPos = palka.get_Parameter(new Guid("92ae0425-031b-40a9-8904-023f7389963b")).AsString();
                    var palkaMark = palka.get_Parameter(new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405")).AsString();
                    var wallThikhness = palka.get_Parameter(new Guid("a506ea75-dab1-4c28-8921-c59c841ebf70")).AsValueString();

                    r.get_Parameter(new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405")).Set($"{palkaPos}-{palkaMark}{wallThikhness}");

                    foreach (var guid in palkaParamGuids)
                    {
                        var rebarPar = r.get_Parameter(guid);
                        var palkaPar = palka.get_Parameter(guid);
                        if (!rebarPar.IsReadOnly && palkaPar.HasValue)
                        {
                            rebarPar.SetValue(palkaPar.GetValue());
                        }
                    }

                    var useScheduleParam = r.get_Parameter(RebarMethods.UseScheduleGuid) ?? r.LookupParameter("Учет в спецификации");

                    if (!useScheduleParam.IsReadOnly)
                        useScheduleParam.Set(0);
                }

                var intUnit = UnitUtils.ConvertFromInternalUnits(1, ParameterMethods.GetUnitType());
                foreach (var rebar in typeRebars.Where(r => r.get_Parameter(_typeGuid).AsString().Contains("Шпилька")))
                {
                    var countType = palka.LookupParameter("Шпилька_Тип").AsString();
                    
                    var vertArmStep = palka.LookupParameter("ВертАрм_Шаг").AsDouble() * intUnit;
                    if (vertArmStep > 99 && vertArmStep < 126 && countType == "Вар 1")
                        vertArmStep = 4 * vertArmStep;
                    else if (vertArmStep <= 200 || countType != "Вар 4")
                        vertArmStep = 2 * vertArmStep;

                    var horArmStep = rebar.get_Parameter(_stepGuid).AsDouble() * intUnit;

                    rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set($"{vertArmStep}x{horArmStep}(h) {countType}");
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}