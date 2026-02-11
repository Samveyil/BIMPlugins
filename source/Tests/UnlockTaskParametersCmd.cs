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
    public class UnlockTaskParametersCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var paramsDict = new Dictionary<Guid, string>
            {
                { new Guid("dd0d08fc-a042-4bb4-bf2a-d366fad18556"), "OLP_Task_Ширина" },
                { new Guid("a07b7c27-984a-4ea4-b701-d8a7cde0ac75"), "OLP_Task_Высота" },
                {new Guid("150f5549-b9dc-4939-9d5f-a345c7cee5c6"), "OLP_Task_Глубина" }
            };

            var famDocs = new List<Document>();

            var families = doc.ToElements<Family>()
                .Where(f => f.FamilyCategoryId == new ElementId(BuiltInCategory.OST_TelephoneDevices))
                .ToList();

            foreach (var family in families)
            {
                var famDoc = doc.EditFamily(family);
                famDocs.Add(famDoc);
                var famManager = famDoc.FamilyManager;

                using (Transaction t = new Transaction(famDoc, "Перепривязать параметры"))
                {
                    t.Start();

                    foreach (var kvp in paramsDict)
                    {
                        var paramGuid = kvp.Key;
                        var paramName = kvp.Value;

                        var taskParam = famManager.get_Parameter(paramGuid);
                        if (taskParam == null)
                            continue;

                        var famParams = famManager.GetParameters();

                        var tempParam = famManager.AddParameter(
                            paramName.Replace("OLP_", ""),
                            taskParam.GetParameterGroup(),
                            taskParam.GetParameterType(),
                            true
                        );

                        famManager.SetFormula(tempParam, paramName);

                        BindParameters(famDoc, famParams, taskParam, tempParam);
                    }

                    t.Commit();
                }
            }

            using (TransactionGroup tGroup = new TransactionGroup(doc, "Разблокировка параметров"))
            {
                tGroup.Start();

                famDocs.ForEach(famDoc => famDoc.LoadFamily(doc, new FamilyLoadOptions()));

                using (Transaction t = new Transaction(doc, "Удалить параметры"))
                {
                    TransactionHandler.SetWarningResolver(t, new WarningSkipper());

                    t.Start();

                    var sharedParamsIds = doc.ToElements<SharedParameterElement>()
                        .Where(p => paramsDict.Keys.Contains(p.GuidValue))
                        .Select(p => p.Id)
                        .ToList();

                    doc.Delete(sharedParamsIds);

                    t.Commit();
                }

                foreach (var famDoc in famDocs)
                {
                    var famManager = famDoc.FamilyManager;

                    using (Transaction t = new Transaction(famDoc, "Разблокировать параметры в семействе"))
                    {
                        t.Start();

                        foreach (var kvp in paramsDict)
                        {
                            var tempParam = famManager.get_Parameter(kvp.Value.Replace("OLP_", ""));
                            if (tempParam == null)
                                continue;

                            famDoc.Delete(famManager.get_Parameter(kvp.Key).Id);

                            var taskParam = famManager.AddParameter(
                                ParameterMethods.FindExternalDefinition(kvp.Value, kvp.Key),
                                tempParam.GetParameterGroup(),
                                true
                            );

                            famManager.SetFormula(taskParam, tempParam.Definition.Name);
                        }

                        t.Commit();
                    }

                    famDoc.LoadFamily(doc, new FamilyLoadOptions());
                }

                foreach (var famDoc in famDocs)
                {
                    var famManager = famDoc.FamilyManager;

                    using (Transaction t = new Transaction(famDoc, "Перепривязать параметры"))
                    {
                        t.Start();

                        foreach (var kvp in paramsDict)
                        {
                            var paramGuid = kvp.Key;
                            var paramName = kvp.Value.Replace("OLP_", "");

                            var taskParam = famManager.get_Parameter(paramGuid);
                            if (taskParam == null)
                                continue;

                            var famParams = famManager.GetParameters();

                            var tempParam = famParams.FirstOrDefault(p => p.Definition.Name == paramName);

                            famManager.SetFormula(taskParam, null);

                            BindParameters(famDoc, famParams, tempParam, taskParam);

                            famDoc.Delete(tempParam.Id);
                        }

                        t.Commit();
                    }

                    famDoc.LoadFamily(doc, new FamilyLoadOptions());
                }

                tGroup.Assimilate();
            }

            return Result.Succeeded;
        }

        private void BindParameters(Document famDoc, IList<FamilyParameter> famParams, FamilyParameter oldParam, FamilyParameter newParam)
        {
            var famManager = famDoc.FamilyManager;

            var oldParamName = oldParam.Definition.Name;
            var newParamName = newParam.Definition.Name;

            var floorHeightParam = famParams.FirstOrDefault(f => f.Definition.Name == "OLP_Task_Высота пола");

            if (floorHeightParam != null)
                famManager.RenameParameter(floorHeightParam, "1");

            foreach (var famParam in famParams)
            {
                if (IsParameterUsedInFormula(famParam.Formula, oldParamName))
                    famManager.SetFormula(famParam, famParam.Formula.Replace(oldParamName, newParamName));
            }

            if (floorHeightParam != null)
                famManager.RenameParameter(floorHeightParam, "OLP_Task_Высота пола");

            var assocParams = oldParam.AssociatedParameters.Cast<Parameter>().ToList();
            foreach (var assocParam in assocParams)
                famManager.AssociateElementParameterToFamilyParameter(assocParam, newParam);

            foreach (var dimen in famDoc.ToElements<Dimension>().Where(d => d.Name.Contains($"Метка: {oldParamName}")))
                dimen.FamilyLabel = newParam;
        }
        private bool IsParameterUsedInFormula(string formula, string paramName)
        {
            if (formula.IsNullOrEmpty())
                return false;

            return formula.Contains(paramName);
        }
    }
}