using Aspose.Cells;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Interfaces;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ColumnLengthCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            var botFloor = RevitAPI.UIDocument.PickObject<Floor>("");
            var topFloor = RevitAPI.UIDocument.PickObject<Floor>("");

            var columns = RevitAPI.UIDocument.PickObjects<FamilyInstance>("");

            var botZ = botFloor.get_BoundingBox(null).Max.Z;
            var topZ = topFloor.get_BoundingBox(null).Min.Z;

            using (Transaction t = new Transaction(doc, "Регулировка высоты"))
            {
                t.Start();

                foreach (var column in columns)
                {
                    column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM).Set(botFloor.LevelId);
                    column.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM).Set(topFloor.LevelId);

                    var columnStartPoint = column.ToLocationCoordinates(ElementExtensions.LocationType.StartPoint).Z;
                    var columnEndPoint = column.ToLocationCoordinates(ElementExtensions.LocationType.EndPoint).Z;

                    column.get_Parameter(BuiltInParameter.SLANTED_COLUMN_BASE_EXTENSION).Set(columnStartPoint - botZ);
                    column.get_Parameter(BuiltInParameter.SLANTED_COLUMN_TOP_EXTENSION).Set(topZ - columnEndPoint);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif