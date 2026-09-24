using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.Linq;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WhoDidCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;
            var doc = uiDoc.Document;

            var selectedElement = uiDoc.ToSelectedElements()
                .FirstOrDefault();

            if (selectedElement == null)
            {
                TaskDialog.Show("Ошибка", "Сначала выберите элемент");
                return Result.Failed;
            }

            var workInfo = WorksharingUtils.GetWorksharingTooltipInfo(doc, selectedElement.Id);

            string creator = workInfo.Creator;
            string owner = workInfo.Owner;
            string lastChange = workInfo.LastChangedBy;
            string to_print = "Создатель: " + creator + "\n" + "Владелец: " + owner + "\n" + "Последнее изменение: " + lastChange;

            TaskDialog.Show("Кто сделал это?", to_print);

            return Result.Succeeded;
        }
    }
}