using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.Windows;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Parameters
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CopyPropertiesCmd : IExternalCommand
    {
        private const string Title = "Копировать свойства";
        private const string TargetPrompt = "Выберите элемент для копирования свойств. Esc — завершить";

        // The type is changed as a whole, not through these parameters.
        private static readonly HashSet<BuiltInParameter> TypeParameters =
        [
            BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM,
            BuiltInParameter.ELEM_FAMILY_PARAM,
            BuiltInParameter.ELEM_TYPE_PARAM,
            BuiltInParameter.SYMBOL_ID_PARAM
        ];

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;
            var doc = uiDoc.Document;

            var hostElement = uiDoc.PickElement("Выберите элемент, с которого нужно копировать свойства");
            if (hostElement == null)
                return Result.Cancelled;

            var typeId = hostElement.GetTypeId();
            var category = hostElement.GetBuiltInCategory();
            var hostParameters = hostElement.GetOrderedParameters()
                .Where(p => !p.IsReadOnly && p.StorageType != StorageType.None)
                .Where(p => !(p.Definition is InternalDefinition definition && TypeParameters.Contains(definition.BuiltInParameter)))
                .ToList();

            // Parameters that would not take the value are told at the end instead of stopping the copying.
            var failed = new List<string>();

            using (TransactionGroup tGroup = new TransactionGroup(doc, Title))
            {
                tGroup.Start();

                while (true)
                {
                    var element = category == BuiltInCategory.INVALID
                        ? uiDoc.PickElement(TargetPrompt)
                        : uiDoc.PickElement(category, TargetPrompt);
                    if (element == null)
                        break;

                    using (Transaction t = new Transaction(doc, Title))
                    {
                        TransactionHandler.SetWarningResolver(t, new WarningSkipper());

                        t.Start();

                        if (typeId != ElementId.InvalidElementId && element.GetTypeId() != typeId && element.IsValidType(typeId))
                            element.ChangeTypeId(typeId);

                        foreach (var hostParameter in hostParameters)
                        {
                            var targetParameter = element.ToParameter(hostParameter.Id);
                            if (targetParameter == null || targetParameter.IsReadOnly || (!hostParameter.HasValue && !targetParameter.HasValue))
                                continue;

                            bool isSet;
                            try
                            {
                                isSet = targetParameter.SetValue(hostParameter.GetValue());
                            }
                            catch (Autodesk.Revit.Exceptions.ApplicationException)
                            {
                                isSet = false;
                            }

                            if (!isSet)
                                failed.Add($"{element.Name} (Id {element.Id}): {hostParameter.Definition.Name}");
                        }

                        t.Commit();
                    }
                }

                tGroup.Assimilate();
            }

            if (failed.Count != 0)
                ReportDialog.Show(failed, "Не удалось скопировать");

            return Result.Succeeded;
        }
    }
}
