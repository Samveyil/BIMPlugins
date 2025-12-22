using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Place2DRebarCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var startPoint = new XYZ();
            var length = UnitUtils.ConvertToInternalUnits(2000, ParameterMethods.GetUnitType());
            var offset = UnitUtils.ConvertToInternalUnits(2500, ParameterMethods.GetUnitType());

            var i = 0;

            var rebarTypes = RevitAPI.Document.ToElements<FamilySymbol>(BuiltInCategory.OST_DetailComponents)
                .OrderBy(t => t.FamilyName)
                .ToList();

            using (Transaction t = new Transaction(RevitAPI.Document, "Создать 2д-арматуру"))
            {
                t.Start();

                foreach (var rebarType in rebarTypes)
                {
                    rebarType.Activate();

                    double X;

                    if (i > 5)
                    {
                        startPoint = new XYZ(0, startPoint.Y - offset, 0);
                        i = 0;
                    }

                    X = startPoint.X + length;

                    try
                    {
                        RevitAPI.Document.Create.NewFamilyInstance(
                            Line.CreateBound(startPoint, new XYZ(X, startPoint.Y, 0)),
                            rebarType,
                            RevitAPI.ActiveView
                        );
                    }
                    catch { continue; }

                    startPoint = new XYZ(X + offset, startPoint.Y, 0);
                    i++;
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}