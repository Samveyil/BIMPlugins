using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
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

            if (!doc.IsWorkshared)
            {
                MessageDialog.Show("Модель не является файлом совместной работы!", "Кто сделал это?", icon: MessageIcon.Warning);
                return Result.Cancelled;
            }

            var selectedElement = uiDoc.ToSelectedElements()
                .FirstOrDefault();

            if (selectedElement == null)
            {
                MessageDialog.Show("Сначала выберите элемент!", "Кто сделал это?", icon: MessageIcon.Warning);
                return Result.Cancelled;
            }

            var workInfo = WorksharingUtils.GetWorksharingTooltipInfo(doc, selectedElement.Id);

            MessageDialog.Show(
                $"Создатель: {workInfo.Creator}\nВладелец: {workInfo.Owner}\nПоследнее изменение: {workInfo.LastChangedBy}",
                "Кто сделал это?", icon: MessageIcon.Information);

            return Result.Succeeded;
        }
    }
}
