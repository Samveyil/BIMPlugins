using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class WhoDidCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ICollection<ElementId> ids = RevitAPI.UIDocument.Selection.GetElementIds();

                var workInfo = WorksharingUtils.GetWorksharingTooltipInfo(RevitAPI.Document, ids.First());
                
                string creator = workInfo.Creator;
                string owner = workInfo.Owner;
                string lastChange = workInfo.LastChangedBy;
                string to_print = "Создатель: " + creator + "\n" + "Владелец: " + owner + "\n" + "Последнее изменение: " + lastChange;
                
                TaskDialog.Show("Кто сделал это?", to_print); 
            }
            catch
            {
                TaskDialog.Show("Ошибка", "Сначала выберите элемент");
            }

            return Result.Succeeded;
        }
    }
}