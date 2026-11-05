using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.FailuresProcessing;
using BIMPlugins.ExtStorage.Interfaces;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;
            var doc = uiDoc.Document;
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

            Console.WriteLine("Халлоу");


            return Result.Succeeded;
        }
    }
}
#endif