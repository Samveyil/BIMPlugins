using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RotateColumnCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            using (Transaction t = new Transaction(doc, "Создать фахверк"))
            {
                t.Start();

                foreach (FamilyInstance slantedColumn in RevitAPI.UIDocument.ToSelectedElements())
                {
                    var colAxis = (slantedColumn.Location as LocationCurve).Curve as Line;

                    ElementTransformUtils.RotateElement(doc, slantedColumn.Id,
                        colAxis,
                        slantedColumn.get_Parameter(new Guid("4f9a558c-61b9-4c38-a08c-a25465aa8abd")).AsDouble() * -2
                    );
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif