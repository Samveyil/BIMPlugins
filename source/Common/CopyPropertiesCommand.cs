using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.ExtStorage.Methods;
using System.Linq;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CopyPropertiesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var hostElement = RevitAPI.UIDocument.PickObject("Выберите элемент, с которого нужно копировать свойства");
            if (hostElement == null) return Result.Cancelled;

            var familyId = hostElement.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).AsElementId();
            var hostParameters = hostElement.GetOrderedParameters().Where(p => !p.IsReadOnly && p.StorageType != StorageType.None);

            using (TransactionGroup tGroup = new TransactionGroup(doc, "Копировать свойства"))
            {
                tGroup.Start();

                while (true)
                {
                    var element = RevitAPI.UIDocument.PickObject(hostElement.GetBuiltInCategory(), "Выберите элемент для копирования свойств");
                    if (element == null) break;

                    using (Transaction t = new Transaction(doc, "Копирование свойств"))
                    {
                        TransactionHandler.SetWarningResolver(t, new WarningSkipper());

                        t.Start();

                        element.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).Set(familyId);

                        foreach (var hostParam in hostParameters)
                        {
                            var elemParam = element.ToParameter(hostParam.Id);

                            if (!hostParam.HasValue && !elemParam.HasValue) continue;

                            elemParam.SetValue(hostParam.GetValue());
                        }

                        t.Commit();
                    }
                }

                tGroup.Assimilate();
            }

            return Result.Succeeded;
        }
    }
}