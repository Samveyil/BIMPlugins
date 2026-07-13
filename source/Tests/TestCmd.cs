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
using System.Windows.Documents;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var columns = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_StructuralColumns)
                .Where(p => p.WorksetId.IntegerValue == 13810)
                .ToList();

            var columnsAR = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_StructuralColumns)
                .Where(p => p.GroupId?.IntegerValue == 26980835)
                .ToList();

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            using (Transaction t = new Transaction(doc, "Создать фахверк"))
            {
                t.Start();

                foreach (var column in columns)
                {
                    var origin = column.ToPoint();
                    var columnAR = columnsAR.FirstOrDefault(c => c.ToPoint().IsAlmostEqualTo(origin));

                    if (columnAR != null)
                    {
                        column.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).Set(columnAR.Symbol.Id);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif