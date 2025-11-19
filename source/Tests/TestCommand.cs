using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Interfaces;
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
            var paramsDict = new Dictionary<Guid, Guid>
            {
                { new Guid("dd0d08fc-a042-4bb4-bf2a-d366fad18556"), new Guid("8f2e4f93-9472-4941-a65d-0ac468fd6a5d") },         // OLP_Task_Ширина, ADSK_Размер_Ширина
                { new Guid("a07b7c27-984a-4ea4-b701-d8a7cde0ac75"), new Guid("da753fe3-ecfa-465b-9a2c-02f55d0c2ff1") },         // OLP_Task_Высота, ADSK_Размер_Высота
            };

            var taskInstanceDict = new Dictionary<FamilyInstance, Dictionary<Guid, double>>();
            foreach (var taskInstance in RevitAPI.Document.ToElements(BuiltInCategory.OST_TelephoneDevices))
            {
                var paramDict = new Dictionary<Guid, double>();
                foreach (var guid in paramsDict.Keys)
                {
                    var param = taskInstance.get_Parameter(guid);
                    if (param != null && param.HasValue)
                        paramDict.Add(guid, param.AsDouble());
                }

                taskInstanceDict.Add((FamilyInstance)taskInstance, paramDict);
            }

            var famDocsPathList = new List<string>()
            {
                @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Отверстие.rfa",
                @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Задание.rfa",
                @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Круглый_Задание.rfa",
                @"\\Diskstation\производство\Ревит\REVIT_SETUP\12_Автоматизация\Families\Tasks\OLP_TASK_Бокс_Круглый_Отверстие.rfa"
            };

            var famDocs = new List<Document>();
            foreach (var path in famDocsPathList)
            {
                famDocs.Add(RevitAPI.Application.OpenDocumentFile(path));
            }

            var famDoc = famDocs[0];
            var famManager = famDoc.FamilyManager;

            using (Transaction t = new Transaction(famDoc, "Привязка размеров"))
            {
                t.Start();

                var dimens = famDoc.ToElements<Dimension>().Where(d => d.Name.Contains("Метка:") && d.FamilyLabel.IsShared).ToList();

                foreach (var kvp in paramsDict)
                {
                    var dimen = dimens.FirstOrDefault(d => d.FamilyLabel.GUID == kvp.Key);
                    if (dimen != null)
                    {
                        dimen.FamilyLabel = famManager.get_Parameter(kvp.Value);
                    }
                }

                t.Commit();
            }

            using (TransactionGroup tGroup = new TransactionGroup(RevitAPI.Document, "Разблокировка параметров"))
            {
                tGroup.Start();

                famDoc.LoadFamily(RevitAPI.Document, new FamilyLoadOptions());

                using (Transaction t = new Transaction(RevitAPI.Document, "Удалить параметры"))
                {
                    t.Start();

                    var sharedParamsIds = RevitAPI.Document.ToElements<SharedParameterElement>()
                        .Where(p => paramsDict.Keys.Contains(p.GuidValue))
                        .Select(p => p.Id)
                        .ToList();

                    RevitAPI.Document.Delete(sharedParamsIds);

                    t.Commit();
                }

                using (Transaction t = new Transaction(famDoc, "Сбросить формулы"))
                {
                    t.Start();

                    foreach (var guid in paramsDict.Values)
                    {
                        var param = famManager.get_Parameter(guid);
                        if (param != null)
                            famManager.SetFormula(param, null);
                    }

                    t.Commit();
                }

                foreach (var taskDock in famDocs)
                {
                    taskDock.LoadFamily(RevitAPI.Document, new FamilyLoadOptions());
                    taskDock.Close(false);
                }

                using (Transaction t = new Transaction(RevitAPI.Document, "Записать значения"))
                {
                    t.Start();

                    foreach (var kvp in taskInstanceDict)
                    {
                        var taskInstance = kvp.Key;
                        foreach (var kvp2 in kvp.Value)
                        {
                            taskInstance.get_Parameter(kvp2.Key)?.Set(kvp2.Value);
                        }
                    }

                    t.Commit();
                }

                famDoc = RevitAPI.Application.OpenDocumentFile(famDocsPathList[0]);
                famDoc.LoadFamily(RevitAPI.Document, new FamilyLoadOptions());
                famDoc.Close(false);

                tGroup.Assimilate();
            }

            return Result.Succeeded;
        }
    }
}