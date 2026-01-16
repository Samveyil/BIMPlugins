using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Methods;
using System;


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

            try
            {
                var references = uidoc.Selection.PickObjects(ObjectType.Face, "Выберите грани");

                double sum = 0;
                foreach (Reference reference in references)
                {
                    Element element = doc.GetElement(reference);

                    GeometryObject geometryObject = element.GetGeometryObjectFromReference(reference);
                    Face face = geometryObject as Face;

                    sum += face.Area;
                }

                TaskDialog.Show("Суммарная площадь", $"{Math.Round(UnitUtils.ConvertFromInternalUnits(sum, ParameterMethods.GetUnitType("m2")), 3)} м2");
            }
            catch { }

            return Result.Succeeded;
        }
    }
}