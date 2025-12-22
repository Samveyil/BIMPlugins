using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.ExtStorage.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            using (Transaction t = new Transaction(RevitAPI.Document, "test"))
            {
                t.Start();

                foreach (var rebar in RevitAPI.Document.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents)
                    .Where(r => r.get_Parameter(new Guid("b220b6e8-254f-479f-95b8-62fc7123b098")) != null && !r.get_Parameter(new Guid("b220b6e8-254f-479f-95b8-62fc7123b098")).IsReadOnly))
                {
                    rebar.get_Parameter(new Guid("b220b6e8-254f-479f-95b8-62fc7123b098")).Set(1);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}