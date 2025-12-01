using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.ExtStorage.Interfaces;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //var doc = RevitAPI.Document;

            //var paramsDict = new Dictionary<Guid, string>
            //{
            //    { new Guid("dd0d08fc-a042-4bb4-bf2a-d366fad18556"), "OLP_Task_Ширина" },
            //    { new Guid("a07b7c27-984a-4ea4-b701-d8a7cde0ac75"), "OLP_Task_Высота" },
            //};

            //var taskInstanceDict = new Dictionary<FamilyInstance, Dictionary<Guid, double>>();
            //foreach (var taskInstance in doc.ToElements<FamilyInstance>(BuiltInCategory.OST_TelephoneDevices))
            //{
            //    var paramDict = new Dictionary<Guid, double>();
            //    foreach (var guid in paramsDict.Keys)
            //    {
            //        var param = taskInstance.get_Parameter(guid);
            //        if (param != null && param.HasValue)
            //            paramDict.Add(guid, param.AsDouble());
            //    }

            //    taskInstanceDict.Add(taskInstance, paramDict);
            //}

            //var famDocsPathList = new List<string>()
            //{
            //    @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Отверстие.rfa",
            //    @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Задание.rfa",
            //    @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Круглый_Задание.rfa",
            //    @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Круглый_Отверстие.rfa"
            //};

            //var famDocs = new List<Document>();
            //foreach (var path in famDocsPathList)
            //{
            //    var famDoc = RevitAPI.Application.OpenDocumentFile(path);
            //    famDocs.Add(famDoc);

            //    if (!famDoc.Title.Contains("Круглый"))
            //    {
            //        var famManager = famDoc.FamilyManager;

            //        using (Transaction t = new Transaction(famDoc, "Перепривязать параметры"))
            //        {
            //            t.Start();

            //            var dimens = famDoc.ToElements<Dimension>().Where(d => d.Name.Contains("Метка:") && d.FamilyLabel.IsShared).ToList();

            //            foreach (var kvp in paramsDict)
            //            {
            //                var dimen = dimens.FirstOrDefault(d => d.FamilyLabel.GUID == kvp.Key);
            //                if (dimen != null)
            //                {
            //                    var oldParameter = dimen.FamilyLabel;

            //                    var labelParam = famManager.AddParameter(
            //                        oldParameter.Definition.Name.Contains("Ширина") ? "Ширина" : "Высота",
            //                        oldParameter.GetParameterGroup(),
            //                        oldParameter.GetParameterType(),
            //                        true
            //                    );

            //                    famManager.SetFormula(labelParam, oldParameter.Definition.Name);

            //                    dimen.FamilyLabel = labelParam;
            //                    dimens.Remove(dimen);
            //                }
            //            }

            //            t.Commit();
            //        }
            //    }
            //}

            //using (TransactionGroup tGroup = new TransactionGroup(doc, "Разблокировка параметров"))
            //{
            //    tGroup.Start();

            //    famDocs.ForEach(famDoc => famDoc.LoadFamily(doc, new FamilyLoadOptions()));

            //    using (Transaction t = new Transaction(doc, "Удалить параметры"))
            //    {
            //        TransactionHandler.SetWarningResolver(t, new WarningSkipper());

            //        t.Start();

            //        var sharedParamsIds = doc.ToElements<SharedParameterElement>()
            //            .Where(p => paramsDict.Keys.Contains(p.GuidValue))
            //            .Select(p => p.Id)
            //            .ToList();

            //        doc.Delete(sharedParamsIds);

            //        t.Commit();
            //    }

            //    foreach (var famDoc in famDocs)
            //    {
            //        if (!famDoc.Title.Contains("Круглый"))
            //        {
            //            var famManager = famDoc.FamilyManager;

            //            using (Transaction t = new Transaction(famDoc, "Сбросить формулы"))
            //            {
            //                t.Start();

            //                foreach (var paramName in paramsDict.Values)
            //                {
            //                    var param = famManager.get_Parameter(paramName.Contains("Ширина") ? "Ширина" : "Высота");

            //                    famManager.SetFormula(param, null);
            //                }

            //                t.Commit();
            //            }
            //        }

            //        famDoc.LoadFamily(doc, new FamilyLoadOptions());
            //        famDoc.Close(false);
            //    }

            //    using (Transaction t = new Transaction(doc, "Записать значения"))
            //    {
            //        t.Start();

            //        foreach (var kvp in taskInstanceDict)
            //        {
            //            var taskInstance = kvp.Key;
            //            foreach (var kvp2 in kvp.Value)
            //            {
            //                taskInstance.get_Parameter(kvp2.Key)?.Set(kvp2.Value);
            //            }
            //        }

            //        t.Commit();
            //    }

            //    foreach (var path in famDocsPathList.Take(2))
            //    {
            //        var famDoc = RevitAPI.Application.OpenDocumentFile(path);
            //        famDoc.LoadFamily(doc, new FamilyLoadOptions());
            //        famDoc.Close(false);
            //    }

            //    tGroup.Assimilate();
            //}

            var level = RevitAPI.Document.ToElements<Level>().FirstOrDefault();

            var startPoint = new XYZ();
            var length = UnitUtils.ConvertToInternalUnits(500, ParameterMethods.GetUnitType());
            var offset = UnitUtils.ConvertToInternalUnits(800, ParameterMethods.GetUnitType());

            var i = 0;

            var rebarTypes = RevitAPI.Document.ToElements<FamilySymbol>(BuiltInCategory.OST_DetailComponents)
                .OrderBy(t => t.FamilyName)
                .ToList();

            using (Transaction t = new Transaction(RevitAPI.Document, "dsa"))
            {
                t.Start();

                foreach (var rebarType in rebarTypes)
                {
                    rebarType.Activate();

                    double X;

                    if (i > 5)
                    {
                        startPoint = new XYZ(0, startPoint.Y - offset, 0);
                        i = 0;
                    }

                    X = startPoint.X + length;

                    RevitAPI.Document.Create.NewFamilyInstance(
                        Line.CreateBound(startPoint, new XYZ(X, startPoint.Y, 0)),
                        rebarType,
                        level,
                        Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                    );

                    startPoint = new XYZ(X + offset, startPoint.Y, 0);
                    i++;
                }

                t.Commit();
            }

            //using (Transaction t = new Transaction(RevitAPI.Document, "dsa"))
            //{
            //    t.Start();

            //    RevitAPI.Document.ProjectInformation.ToParameter("OLP_Версия шаблона").Set("1.2.28");

            //    t.Commit();
            //}

            return Result.Succeeded;
        }
    }
}