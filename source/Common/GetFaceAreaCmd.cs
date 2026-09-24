using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;

namespace BIMPlugins.Common
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GetFaceAreaCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {            
            Document doc = RevitAPI.Document;
            UIDocument uidoc = RevitAPI.UIDocument;

            var elementRefs = uidoc.PickObjects(ObjectType.Face, "Выберите грани");
            if (elementRefs == null)
                return Result.Cancelled;

            double sum = 0;
            foreach (Reference reference in elementRefs)
            {
                GeometryObject geometryObject = reference.ToElement(doc).GetGeometryObjectFromReference(reference);
                Face face = geometryObject as Face;

                sum += face.Area;
            }

            TaskDialog.Show("Суммарная площадь", $"{sum.ToUnit("m2").Round(3)} м2");

            return Result.Succeeded;
        }
    }
}