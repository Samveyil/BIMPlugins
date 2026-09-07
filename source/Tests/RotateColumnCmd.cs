using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using System;

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

            using (Transaction t = new Transaction(doc, "Создать фахверк"))
            {
                t.Start();

                foreach (FamilyInstance slantedColumn in RevitAPI.UIDocument.ToSelectedElements())
                {
                    var colAxis = (slantedColumn.Location as LocationCurve).Curve as Line;

                    slantedColumn.Rotate(
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