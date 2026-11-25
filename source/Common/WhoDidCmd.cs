using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using System.Linq;
using System.Windows;

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
                MessageWindow.ShowMessage("Модель не является файлом совместной работы!", "Кто сделал это?", icon: MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var selectedElement = uiDoc.ToSelectedElements()
                .FirstOrDefault();

            if (selectedElement == null)
            {
                MessageWindow.ShowMessage("Сначала выберите элемент!", "Кто сделал это?", icon: MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            var workInfo = WorksharingUtils.GetWorksharingTooltipInfo(doc, selectedElement.Id);

            MessageWindow.ShowMessage(
                $"Создатель: {workInfo.Creator}\nВладелец: {workInfo.Owner}\nПоследнее изменение: {workInfo.LastChangedBy}",
                "Кто сделал это?", icon: MessageBoxImage.Information);

            return Result.Succeeded;
        }
    }
}
