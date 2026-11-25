using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Windows;
using System.Linq;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GetFaceAreaCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;
            var doc = uiDoc.Document;

            var references = uiDoc.PickObjects(ObjectType.Face, "Выберите грани");
            if (references == null)
                return Result.Cancelled;

            var area = references
                .Sum(r => ((Face)r.ToElement(doc).GetGeometryObjectFromReference(r)).Area);

            MessageDialog.Show($"{area.ToUnit("m2").Round(3)} м2", "Суммарная площадь", icon: MessageIcon.Information);

            return Result.Succeeded;
        }
    }
}
