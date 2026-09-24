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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

            var dllFolderPath = @"D:\BIM-Плагины\BIMPlugins\source\bin\Release\2019";

            foreach (string file in Directory.GetFiles(dllFolderPath, "*.*", SearchOption.AllDirectories))
            {
                string relativePath = GetRelativePath(dllFolderPath, file);


                Debug.WriteLine(relativePath);
            }

                //var famManager = doc.FamilyManager;

                //var parameters = famManager.GetParameters();
                //foreach (var parameter in parameters.Where(p => !p.Formula.IsNullOrEmpty() && p.Formula.Contains("size_lookup")))
                //{
                //    var formula = parameter.Formula;

                //    foreach (var paramName in parameters.Select(p => p.Definition.Name))
                //    {
                //        if (ContainsParameterName(formula, paramName) && !famManager.get_Parameter(paramName).IsDeterminedByFormula)
                //        {
                //            Debug.WriteLine(paramName);
                //        }
                //    }
                //}


                return Result.Succeeded;
        }

        public static string GetRelativePath(
        string relativeTo,
        string path)
        {
            string basePath = Path.GetFullPath(relativeTo);

            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            string targetPath = Path.GetFullPath(path);

            var baseUri = new Uri(basePath);
            var targetUri = new Uri(targetPath);

            if (!string.Equals(
                baseUri.Scheme,
                targetUri.Scheme,
                StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            string relativePath = Uri.UnescapeDataString(
                baseUri.MakeRelativeUri(targetUri).ToString());

            return relativePath.Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar);
        }
    }
}
#endif