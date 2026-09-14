using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            var intUnit = 1d.FromMillimeters();

            //Solid solid = RevitAPI.UIDocument.PickObject("Выбрать solid").ToSolid();

            //var famDoc = RevitAPI.Application.Documents.Cast<Document>().First(d => d.Title == "Подрезка парапета пристроек.rfa");
            //using (Transaction t = new Transaction(famDoc, "Создать FreeForm"))
            //{
            //    t.Start();

            //    var ffe = FreeFormElement.Create(famDoc, solid);
            //    ffe.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING).Set(1);

            //    t.Commit();
            //}

            //famDoc.PurgeUnused();

            var worksetFilter = new ElementWorksetFilter(new WorksetId(39741));

            var worksetElements = doc.ToElements(worksetFilter);

            return Result.Succeeded;
        }
    }
}
#endif