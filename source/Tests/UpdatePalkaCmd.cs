using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdatePalkaCmd : IExternalCommand
    {
        private Guid _idGuid = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");                    // OLP_Id
        private Guid _typeGuid = new Guid("215d6c56-3700-4db9-a5f5-53ec85b36daa");                  // OLP_Зона расположения
        private Guid _useScheduleGuid = new Guid("b220b6e8-254f-479f-95b8-62fc7123b098");           // OLP_Учет в спецификации

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
                new Guid("5d369dfb-17a2-4ae2-a1a1-bdfc33ba7405"),           // ADSK_Марка конструкции
                new Guid("b5aee52e-5294-46e8-8086-f76421185a84"),           // OLP_Количество конструкций
                new Guid("0134e43b-3fd9-40bb-9abd-41fa4f5b6481"),           // OLP_Количество сборок
                new Guid("5776fb34-04bb-4a41-8f43-81edd9b0daff"),           // OLP_Специфицировать сборку
                new Guid("f59036bb-fa3d-40a8-bcf6-f6bbc54f26b6")            // OLP_Этаж
            };

            var idParamId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == _idGuid).Id;

            var palkaId = doc.ActiveView.get_Parameter(_idGuid).AsString();
            if (palkaId.IsNullOrEmpty())
                return Result.Cancelled;

            var palka = new ElementId(int.Parse(palkaId)).ToElement<FamilyInstance>();

            var idParamFilter = idParamId.CreateEqualsFilter(palkaId);

            var rebars = doc.ToElements(BuiltInCategory.OST_DetailComponents, idParamFilter)
                .GroupBy(r => r.get_Parameter(_typeGuid).AsString())
                .Select(g => g.First())
                .Where(r => !r.get_Parameter(_typeGuid).AsString().IsNullOrEmpty());

            var diametrDict = new Dictionary<string, double>();

            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                foreach (var visibleParam in palka.Parameters.Cast<Parameter>().Where(p => p.Definition.Name.EndsWith(".Вкл") && !p.IsReadOnly))
                    visibleParam.Set(0);

                doc.Regenerate();

                var types = rebars.Select(r => r.get_Parameter(_typeGuid).AsString());
                var hasTopRebar = types.Any(t => t == "ГорАрм_ДопШагСверху");
                var hasBottomRebar = types.Any(t => t == "ГорАрм_ДопШагСнизу");
    
                palka.LookupParameter("ГорАрм_ДопШагСверху.Вкл").Set(hasTopRebar ? 1 : 0);
                palka.LookupParameter("ГорАрм_ДопШагСнизу.Вкл").Set(hasBottomRebar ? 1 : 0);

                foreach (var r in rebars)
                {
                    var rType = r.get_Parameter(_typeGuid).AsString();

                    var visParam = palka.LookupParameter($"{rType}.Вкл");
                    if (visParam != null)
                        visParam.Set(1);

                    if (rType == "ГорАрм_ДопШагСнизу" || rType == "ГорАрм_ДопШагСверху")
                    {
                        palka.LookupParameter(rType).Set(r.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble());

                        var countArray = r.LookupParameter("Колво.Расч");
                        if (countArray == null)
                            continue;
                        
                        if (rType == "ГорАрм_ДопШагСнизу")
                        {
                            palka.LookupParameter($"OLP_ГорАрм_Нижняя зона учащения")
                                .Set(r.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble() * countArray.AsInteger());
                        }
                        else
                        {
                            palka.LookupParameter($"OLP_ГорАрм_Верхняя зона учащения")
                                .Set(r.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble() * countArray.AsInteger());
                        }
                    }

                    rType = rType.Split('_')[0];

                    foreach (var kvp in palkaTypeDic.Skip(1))
                    {
                        var palkaParam = palka.Parameters.Cast<Parameter>()
                            .FirstOrDefault(p => p.Definition.Name == $"{rType}{kvp.Key}" || p.Definition.Name == $"OLP_{rType}{kvp.Key}");
                        if (palkaParam == null)
                            continue;

                        palkaParam.SetValue(r.get_Parameter(kvp.Value).GetValue());
                    }
                }

                doc.Regenerate();

                rebars = doc.ToElements(BuiltInCategory.OST_DetailComponents, idParamFilter);
                foreach (var r in rebars)
                {
                    var rType = r.get_Parameter(_typeGuid).AsString();
                    if (rType.IsNullOrEmpty())
                        continue;
                    else
                        rType = rType.Split('_')[0];

                    var palkaParam = palka.Parameters.Cast<Parameter>()
                        .FirstOrDefault(p => p.Definition.Name == $"{rType}_Количество");
                    if (palkaParam == null)
                        continue;

                    var targetParam = r.get_Parameter(palkaTypeDic["_Количество"]);
                    if (targetParam == null || targetParam.IsReadOnly)
                    {
                        if (r.LookupParameter("Количество_Вручную") != null)
                            r.LookupParameter("Количество_Вручную").Set(palkaParam.AsDouble());
                        else
                        {
                            r.LookupParameter("Задать количество").Set(1);
                            r.LookupParameter("Количество").Set(palkaParam.AsDouble());
                        }
                    }
                    else if (!targetParam.IsReadOnly)
                        targetParam.Set(palkaParam.AsDouble());

                    foreach (var guid in palkaParamGuids)
                    {
                        var rebarPar = r.get_Parameter(guid);
                        var palkaPar = palka.get_Parameter(guid);
                        if (!rebarPar.IsReadOnly && palkaPar.HasValue)
                        {
                            rebarPar.SetValue(palkaPar.GetValue());
                        }
                    }
                }

                foreach (var r in rebars)
                {
                    var p = r.get_Parameter(_useScheduleGuid) ?? r.LookupParameter("Учет в спецификации");
                    
                    if (!p.IsReadOnly)
                        p.Set(0);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}