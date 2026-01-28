using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MakeWallSectionTestCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var palkaTypeDic = new Dictionary<string, Guid>()
            {
                {"_Шаг", new Guid("5d7cb726-ac59-4f05-a902-8fdffa796d15")},
                {"_Диаметр", new Guid("a13859b3-a733-4df0-ba54-ba74966408e9") },
            };

            var palkaId = 25632597;
            var palka = new ElementId(palkaId).ToElement<FamilyInstance>();

            var rebars = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents)
                .Where(r => r.LookupParameter("Марка").AsString() == palkaId.ToString() && !r.LookupParameter("Комментарии").AsString().IsNullOrEmpty())
                .GroupBy(r => r.LookupParameter("Комментарии").AsString())
                .Select(g => g.First())
                .ToList();

            var isBottomArm = false;
            var isTopArm = false;

            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                foreach (var r in rebars)
                {
                    var rType = r.LookupParameter("Комментарии").AsString();
                    foreach (var kvp in palkaTypeDic)
                    {
                        var param = r.get_Parameter(kvp.Value);
                        if (param == null)
                            continue;

                        var palkaParam = palka.Parameters.Cast<Parameter>()
                            .FirstOrDefault(p => p.Definition.Name == $"{rType}{kvp.Key}" || p.Definition.Name == $"OLP_{rType}{kvp.Key}");
                        if (palkaParam == null)
                            continue;

                        palkaParam.SetValue(param.GetValue());
                    }

                    if (rType == "ГорАрм_ДопШагСнизу")
                    {
                        isBottomArm = true;

                        palka.LookupParameter("ГорАрм_ДопШагСнизу.Вкл").Set(1);
                        palka.LookupParameter("ГорАрм_ДопШагСнизу").Set(r.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble());

                        var lengthParam = r.LookupParameter("Длина");
                        if (lengthParam != null)
                            palka.LookupParameter("OLP_ГорАрм_Нижняя зона учащения").Set(lengthParam.AsDouble());
                    }
                    else if (rType == "ГорАрм_ДопШагСверху")
                    {
                        isTopArm = true;

                        palka.LookupParameter("ГорАрм_ДопШагСверху.Вкл").Set(1);
                        palka.LookupParameter("ГорАрм_ДопШагСверху").Set(r.get_Parameter(palkaTypeDic["_Шаг"]).AsDouble());

                        var lengthParam = r.LookupParameter("Длина");
                        if (lengthParam != null)
                            palka.LookupParameter("OLP_ГорАрм_Верхняя зона учащения").Set(lengthParam.AsDouble());
                    }
                }

                if (!isBottomArm)
                    palka.LookupParameter("ГорАрм_ДопШагСнизу.Вкл").Set(0);
                if (!isTopArm)
                    palka.LookupParameter("ГорАрм_ДопШагСверху.Вкл").Set(0);

                t.Commit();
            }

            return Result.Succeeded;
        }

        
    }
}