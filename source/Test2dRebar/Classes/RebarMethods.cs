using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Test2dRebar.Classes
{
    public static class RebarMethods
    {
        public static Guid IdGuid { get; } = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");                 // OLP_Id
        public static Guid TypeGuid { get; } = new Guid("215d6c56-3700-4db9-a5f5-53ec85b36daa");               // OLP_Зона расположения
        public static Guid StepGuid { get; } = new Guid("5d7cb726-ac59-4f05-a902-8fdffa796d15");               // ADSK_Шаг элементов
        public static Guid UseScheduleGuid { get; } = new Guid("b220b6e8-254f-479f-95b8-62fc7123b098");        // OLP_Учет в спецификации
        public static Guid DimenAGuid { get; } = new Guid("b10d2260-5080-470d-be69-e136df3b45f6");             // OLP_Арм_Аdef
        public static Guid FormGuid { get; } = new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d");               // OLP_Арм_Форма
        public static Guid PrefixGuid { get; } = new Guid("dce379c0-5e32-4695-b16a-d76ef0100172");             // OLP_Арм_Позиция_Префикс
        public static Guid RazdelGuid { get; } = new Guid("e1b06433-f527-403c-8986-af9a01e6be7f");             // ADSK_Комплект чертежей
        public static Guid NumberGuid { get; } = new Guid("92ae0425-031b-40a9-8904-023f7389963b");             // ADSK_Марка изделия
        public static Guid StructureMarkGuid { get; } = new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405");      // ADSK_Марка конструкции
        public static Guid WallThiknessGuid { get; } = new Guid("a506ea75-dab1-4c28-8921-c59c841ebf70");       // OLP_Толщина стены

        public static Dictionary<string, Guid> PalkaTypeDic { get; } = new Dictionary<string, Guid>()
        {
            {"_Количество",  new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a")},
            {"_Диаметр", new Guid("a13859b3-a733-4df0-ba54-ba74966408e9") },
            {"_Шаг", StepGuid}
        };
        public static List<Guid> PalkaParamGuids { get; } = new List<Guid>
        {
            RazdelGuid,
            NumberGuid,
            new Guid("b5aee52e-5294-46e8-8086-f76421185a84"),                                                   // OLP_Количество конструкций
            /*new Guid("0134e43b-3fd9-40bb-9abd-41fa4f5b6481"),*/                                               // OLP_Количество сборок
            new Guid("5776fb34-04bb-4a41-8f43-81edd9b0daff"),                                                   // OLP_Специфицировать сборку
            new Guid("f59036bb-fa3d-40a8-bcf6-f6bbc54f26b6")                                                    // OLP_Этаж
        };

        public static Parameter GetSymbolParameter(this FamilyInstance rebar, Guid paramGuid)
        {
            var sourceFormParam = rebar.Symbol.get_Parameter(paramGuid);
            sourceFormParam ??= rebar.GetSubComponentIds()
                .FirstOrDefault()?
                .ToElement<FamilyInstance>()
                .Symbol.get_Parameter(paramGuid);

            return sourceFormParam;
        }
        public static bool IsAboveCenter(this FamilyInstance rebar, XYZ midlPoint, XYZ upDirect)
        {
            XYZ relativeVector = rebar.ToLocationCoordinates(ElementExtensions.LocationType.StartPoint) - midlPoint;
            double projection = relativeVector.DotProduct(upDirect);

            return projection > 0.001;
        }

        public static void UpdateElements(Document doc, ElementId idParamId, View view)
        {
            var ids = view.get_Parameter(IdGuid).AsString();
            if (ids.IsNullOrEmpty())
                return;

            var idParamFilter = idParamId.CreateEqualsFilter(ids);

            var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents, idParamFilter)
                .Where(r => !r.get_Parameter(TypeGuid).AsString().IsNullOrEmpty())
                .ToList();

            XYZ edgeRebarUpViewDirect = null;
            List<IGrouping<RebarGroupKey, FamilyInstance>> sectionGroups = [];

            var edgeRebar = rebars.FirstOrDefault(r => r.get_Parameter(TypeGuid).AsString().Contains("ВертАрмТорца") || r.get_Parameter(TypeGuid).AsString() == "ГорПка");
            if (edgeRebar != null)
            {
                var edgeRebarView = edgeRebar.OwnerViewId.ToElement<View>();
                edgeRebarUpViewDirect = edgeRebarView.UpDirection.Normalize();

                var wall = doc.ToElements<Wall>(edgeRebarView.Id)
                    .FirstOrDefault(w => (w.Location as LocationCurve).Curve is Line line &&
                        (line.Direction.Normalize().IsAlmostEqualTo(edgeRebarUpViewDirect) || line.Direction.Normalize().IsAlmostEqualTo(edgeRebarUpViewDirect.Negate())));

                var wallMidlPoint = (wall.Location as LocationCurve).Curve.Evaluate(0.5, true);

                sectionGroups = rebars
                    .Where(r => r.get_Parameter(TypeGuid).AsString().Contains("ВертАрмТорца") || r.get_Parameter(TypeGuid).AsString() == "ГорПка")
                    .GroupBy(r => new RebarGroupKey(r.get_Parameter(TypeGuid).AsString(), r.IsAboveCenter(wallMidlPoint, edgeRebarUpViewDirect)))
                    .ToList();
            }

            var typeRebars = rebars.GroupBy(r => r.get_Parameter(TypeGuid).AsString())
                .Select(g => g.First())
                .ToList();

            var types = typeRebars.Select(r => r.get_Parameter(TypeGuid).AsString());
            var hasTopRebar = types.Any(t => t == "ГорАрм_ДопШагСверху");
            var hasBottomRebar = types.Any(t => t == "ГорАрм_ДопШагСнизу");

            var countDict = new Dictionary<string, double>();

            FamilyInstance palka = null;
            using (Transaction t = new Transaction(doc, "Передать из палки"))
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
                        var rType = typeRebar.get_Parameter(TypeGuid).AsString();

                        var visParam = palka.LookupParameter($"{rType}.Вкл");
                        visParam?.Set(1);

                        if (rType == "ГорАрм_ДопШагСнизу" || rType == "ГорАрм_ДопШагСверху")
                        {
                            palka.LookupParameter(rType).Set(typeRebar.get_Parameter(PalkaTypeDic["_Шаг"]).AsDouble());

                            var countArray = typeRebar.LookupParameter("Колво.Расч");
                            if (countArray == null)
                                continue;

                            if (rType == "ГорАрм_ДопШагСнизу")
                            {
                                palka.LookupParameter($"OLP_ГорАрм_Нижняя зона учащения")
                                    .Set(typeRebar.get_Parameter(PalkaTypeDic["_Шаг"]).AsDouble() * countArray.AsInteger());
                            }
                            else
                            {
                                palka.LookupParameter($"OLP_ГорАрм_Верхняя зона учащения")
                                    .Set(typeRebar.get_Parameter(PalkaTypeDic["_Шаг"]).AsDouble() * countArray.AsInteger());
                            }
                        }

                        if (!rType.Contains("ГорАрм") && (!rType.Contains("ВертАрм") || rType.Contains("ВертАрмТорца")))
                            rType = rType.Split('_')[0];

                        if (palka.Symbol.FamilyName.Contains("Пилон") && rType == "ГорАрм")
                        {
                            if (typeRebar.GetSymbolParameter(RebarMethods.PrefixGuid).AsString() == "Х")
                                palka.LookupParameter("Хомут.Вкл").Set(1);
                        }

                        foreach (var kvp in PalkaTypeDic.Skip(1))
                        {
                            var palkaParam = palka.Parameters.Cast<Parameter>()
                                .FirstOrDefault(p => p.Definition.Name == $"{rType}{kvp.Key}");
                            if (palkaParam == null)
                                continue;

                            palkaParam.SetValue(typeRebar.get_Parameter(kvp.Value).GetValue());
                        }
                    }

                    doc.Regenerate();

                    foreach (var group in typeRebars.GroupBy(r => r.get_Parameter(TypeGuid).AsString().Split('_')[0]))
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
                    .Where(r => r.get_Parameter(TypeGuid).AsString().Contains("ВертАрм_Доп"))
                    .ToList();

                if (dopRebars.Count != 0)
                {
                    if (!countDict.ContainsKey("ВертАрм_Доп"))
                        countDict["ВертАрм_Доп"] = 0;

                    if (dopRebars.FirstOrDefault(r => r.get_Parameter(TypeGuid).AsString() == "ВертАрм_Доп_2ряд") != null)
                    {
                        countDict["ВертАрм_Доп"] += (countDict["ВертАрм"] / 2).Round(0);
                    }
                    else if (rebars.Where(r => r.get_Parameter(TypeGuid).AsString().Contains("ВертАрм_Доп")).GroupBy(r => r.OwnerViewId).FirstOrDefault(g => g.Count() == 1) != null)
                    {
                        countDict["ВертАрм_Доп"] += (countDict["ВертАрм"] / 2).Round(0);
                    }
                    else
                    {
                        palka.LookupParameter("ВертАрм_Доп_2ряд_Диаметр").Set(palka.LookupParameter("ВертАрм_Доп_Диаметр").AsDouble());

                        countDict["ВертАрм_Доп"] += countDict["ВертАрм"];
                    }
                }

                if (typeRebars.FirstOrDefault(r => r.get_Parameter(TypeGuid).AsString() == "ВертАрм_2ряд") != null)
                    countDict["ВертАрм"] = (countDict["ВертАрм"] / 2).Round(0);

                /// Корректировка кол-ва ВертАрмТорца
                foreach (var sectionGroup in sectionGroups.Where(g => g.Key.Type != "ГорПка"))
                {
                    countDict[sectionGroup.Key.Type] = sectionGroup.Count();
                }

                /// Корректировка кол-ва ВертПка
                if (rebars.Where(r => r.get_Parameter(TypeGuid).AsString() == "ВертПка").Count() > 1)
                    countDict["ВертПка"] = countDict["ВертПка"] * 2;

                foreach (var r in rebars)
                {
                    var rType = r.get_Parameter(TypeGuid).AsString();

                    if (rType.Contains("ВертАрм_Доп"))
                        rType = "ВертАрм_Доп";
                    else
                        rType = rType.Split('_')[0];

                    if (!countDict.ContainsKey(rType))
                        continue;

                    var targetParam = r.get_Parameter(PalkaTypeDic["_Количество"]);
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

                    r.get_Parameter(StructureMarkGuid).Set(GetWallMark(palka, palka.get_Parameter(NumberGuid).AsString()));

                    foreach (var guid in PalkaParamGuids)
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
                foreach (var rebar in typeRebars.Where(r => r.get_Parameter(TypeGuid).AsString().Contains("Шпилька")))
                {
                    var countType = palka.LookupParameter("Шпилька_Тип").AsString();

                    var vertArmStep = palka.LookupParameter("ВертАрм_Шаг").AsDouble() * intUnit;
                    if (vertArmStep > 99 && vertArmStep < 126 && countType == "Вар 1")
                        vertArmStep = 4 * vertArmStep;
                    else if (vertArmStep <= 200 || countType != "Вар 4")
                        vertArmStep = 2 * vertArmStep;

                    var horArmStep = rebar.get_Parameter(StepGuid).AsDouble() * intUnit;

                    rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set($"{vertArmStep}x{horArmStep}(h) {countType}");
                }

                t.Commit();
            }
        }

        public static void CreateViewSection(List<Element> palkas)
        {
            var sectionType = RevitAPI.Document.ToElements<ViewFamilyType>().FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);
            var detailType = RevitAPI.Document.ToElements<ViewFamilyType>().FirstOrDefault(v => v.ViewFamily == ViewFamily.Detail);

            var wall = RevitAPI.UIDocument.PickObject<Wall>("Выберите стену");
            var wallCurve = (wall.Location as LocationCurve).Curve;
            if (wallCurve is not Line)
            {
                TaskDialog.Show("Ошибка", "Стена должна быть прямолинейной");
                return;
            }

            var wallLine = wallCurve as Line;
            var wallDirection = wallLine.Direction;
            var perpDirection = new XYZ(-wallDirection.Y, wallDirection.X, 0);

            var wallHeight = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM).AsDouble();
            var wallLength = wallLine.Length;
            var wallWidth = wall.Width;

            var midPoint = wallLine.Evaluate(0.5, true);
            var ZCoord = wall.get_BoundingBox(null).Min.Z;

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            var sectionDepth = 300 * intUnit;

            var sectionTransform = Transform.Identity;
            sectionTransform.Origin = midPoint + new XYZ(0, 0, ZCoord);
            sectionTransform.BasisX = perpDirection;
            sectionTransform.BasisY = XYZ.BasisZ;
            sectionTransform.BasisZ = wallDirection;

            var sectionBox = new BoundingBoxXYZ()
            {
                Transform = sectionTransform,
                Min = new XYZ(-(wallWidth / 2 + 600 * intUnit), -600 * intUnit, 0),
                Max = new XYZ(wallWidth / 2 + 600 * intUnit, wallHeight + 600 * intUnit, sectionDepth)
            };

            var detailTransform = Transform.Identity;
            detailTransform.Origin = midPoint + new XYZ(0, 0, ZCoord + wallHeight / 2);
            detailTransform.BasisX = perpDirection;
            detailTransform.BasisY = wallDirection;
            detailTransform.BasisZ = XYZ.BasisZ.Negate();

            var detailBox = new BoundingBoxXYZ()
            {
                Transform = detailTransform,
                Min = new XYZ(-(wallWidth / 2 + 300 * intUnit), -(wallLength / 2 + 300 * intUnit), 0),
                Max = new XYZ(wallWidth / 2 + 300 * intUnit, wallLength / 2 + 300 * intUnit, sectionDepth)
            };

            var palkaIds = palkas.Select(p => p.Id.ToString()).ToList();

            var palka = palkas[0];
            var razdel = palka.get_Parameter(RazdelGuid).AsString();
            var palkaNumber = palka.get_Parameter(NumberGuid).AsString().IsNullOrEmpty() ? GetNumber(razdel).ToString() : palka.get_Parameter(NumberGuid).AsString();
            var wallMark = GetWallMark(palka, palkaNumber);

            ViewSection viewSection;
            ViewSection viewDetail;

            using (TransactionGroup tGroup = new TransactionGroup(RevitAPI.Document, "Создать сечение"))
            {
                tGroup.Start();

                using (Transaction t = new Transaction(RevitAPI.Document, "Создать сечение"))
                {
                    t.Start();

                    viewSection = ViewSection.CreateSection(RevitAPI.Document, sectionType.Id, sectionBox);
                    viewSection.get_Parameter(RazdelGuid).Set(palkas[0].get_Parameter(RazdelGuid).AsString());
                    SetViewName(viewSection,$"21_{razdel}_{wallMark}_Сеч");

                    viewDetail = ViewSection.CreateDetail(RevitAPI.Document, detailType.Id, detailBox);
                    viewDetail.get_Parameter(RazdelGuid).Set(palkas[0].get_Parameter(RazdelGuid).AsString());
                    SetViewName(viewDetail, $"21_{razdel}_{wallMark}_Узел");

                    t.Commit();
                }

                using (Transaction t = new Transaction(RevitAPI.Document, "Присвоить OLP_Id"))
                {
                    t.Start();

                    viewSection.get_Parameter(IdGuid).Set(string.Join(";", palkaIds));
                    viewDetail.get_Parameter(IdGuid).Set(string.Join(";", palkaIds));

                    var newViewIds = string.Join(";", viewSection.Id.ToString(), viewDetail.Id.ToString());
                    foreach (var pal in palkas)
                    {
                        pal.get_Parameter(NumberGuid).Set(palkaNumber.ToString());
                        pal.get_Parameter(IdGuid).Set(newViewIds);
                    }

                    t.Commit();
                }

                tGroup.Assimilate();
            }

            RevitAPI.UIDocument.ActiveView = viewSection;
        }
        public static int GetNumber(string razdel)
        {
            var numbers = RevitAPI.Document.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents)
                .Where(r => r.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString().StartsWith("285") && !r.get_Parameter(NumberGuid).AsString().IsNullOrEmpty() &&
                            r.get_Parameter(RazdelGuid).AsString() == razdel)
                .Select(p =>
                {
                    string numStr = p.get_Parameter(NumberGuid).AsString();
                    int num;
                    return int.TryParse(numStr, out num) ? num : (int?)null;
                })
                .Where(n => n.HasValue)
                .Select(n => n.Value)
                .ToList();

            if (!numbers.Any())
                return 1;
            else
            {
                numbers.Sort();

                int expectedNumber = 1;
                foreach (int num in numbers)
                {
                    if (num > expectedNumber)
                    {
                        return expectedNumber;
                    }
                    expectedNumber = num + 1;
                }

                return numbers.Max() + 1;
            }
        }
        public static void SetViewName(View view, string baseName)
        {
            string currentName = baseName;
            int counter = 0;

            while (true)
            {
                try
                {
                    view.Name = currentName;
                    break;
                }
                catch (Exception)
                {
                    counter++;

                    currentName = new string('!', counter) + baseName;

                    if (counter > 10)
                    {
                        throw new Exception("Не удалось найти свободное имя после 10 попыток.");
                    }
                }
            }
        }

        public static string GetWallMark(Element palka, string palkaNumber)
        {
            var structureMark = palka.get_Parameter(StructureMarkGuid).AsString();
            var wallThikness = palka.get_Parameter(WallThiknessGuid).AsValueString();

            return $"{palkaNumber}-{structureMark}{wallThikness}";
        }
    }
}