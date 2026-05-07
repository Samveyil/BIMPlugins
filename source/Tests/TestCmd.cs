using Autodesk.Revit.Attributes;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
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

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            //var result = string.Empty;

            //var invalidShParams = doc.ToElements<SharedParameterElement>()
            //    .Where(p => p.GetDefinition().ParameterGroup == BuiltInParameterGroup.INVALID)
            //    .Select(p => p.Name)
            //    .ToList();

            //using (RevitProgressBar progressBar = new RevitProgressBar(true))
            //{
            //    progressBar.Run("Поиск параметра...", doc.ToElements<Family>().Where(f => f.IsUserCreated && !f.IsInPlace), (fam) =>
            //    {
            //        var famDoc = doc.EditFamily(fam);

            //        var parameters = famDoc.FamilyManager.GetParameters()
            //            .Select(p => p.Definition.Name)
            //            .ToList();

            //        var common = parameters.Intersect(invalidShParams).ToList();
            //        if (common.Count > 0)
            //            result += $"{famDoc.Title} " + string.Join(", ", common) + "\n";

            //        famDoc.Close(false);
            //    });

            //    if (progressBar.IsCancelling())
            //    {
            //        return Result.Cancelled;
            //    }
            //}

            //TaskDialog.Show("Инфо", result);

            //var paramNames = new List<string>
            //{
            //    "ADSK_Код металлопроката",
            //    "#Арматура_Код металлопроката",
            //    "#Проволка_Код металлопроката"
            //};

            //var famFolderPath = @"C:\Users\shibliev\Desktop\Test";

            //var saveOpt = new SaveAsOptions()
            //{
            //    MaximumBackups = 1
            //};

            //foreach (var filePath in Directory.GetFiles(famFolderPath))
            //{
            //    var famDoc = RevitAPI.Application.OpenDocumentFile(filePath);
            //    //var famDoc = RevitAPI.Document;
            //    var famManager = famDoc.FamilyManager;

            //    var parameters = famManager.GetParameters().ToList();

            //    using (Transaction t = new Transaction(famDoc, "test"))
            //    {
            //        t.Start();

            //        var famInst = famDoc.ToElements<FamilyInstance>(new ElementId(BuiltInParameter.ELEM_FAMILY_PARAM)
            //            .CreateBeginsWithFilter("280_Условный стержень для маркировки")).FirstOrDefault();

            //        var Inst = famDoc.ToElements<FamilyInstance>(new ElementId(BuiltInParameter.ELEM_FAMILY_PARAM)
            //            .CreateBeginsWithFilter("280_Стержень")).FirstOrDefault();

            //        famInst.get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d"))
            //            .SetValue(Inst.ToElementType(famDoc).get_Parameter(new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d")).GetValue());

            //        t.Commit();
            //    }

            //    famDoc.PurgeUnused();

            //    var newPath = filePath.Replace(".rfa", "1.rfa");

            //    saveOpt.PreviewViewId = famDoc.ToElements<View>().FirstOrDefault(v => v.Name == "Опорный уровень").Id;

            //    famDoc.SaveAs(newPath, saveOpt);

            //    famDoc.Close(false);

            //    File.Delete(filePath);
            //    File.Move(newPath, filePath);
            //}

            //var view = doc.ActiveView;

            //var filters = view.GetFilters();

            //var patternId = new FilteredElementCollector(RevitAPI.Document)
            //    .OfClass(typeof(FillPatternElement))
            //    .FirstOrDefault(e => e.Name == "<Сплошная заливка>")
            //    .Id;

            //var elev = UnitUtils.ConvertToInternalUnits(90000, ParameterMethods.GetUnitType());

            var intUnit = UnitUtils.ConvertToInternalUnits(1, ParameterMethods.GetUnitType());

            //using (Transaction t = new Transaction(doc, "test"))
            //{
            //    t.Start();

            //    //var level = Level.Create(doc, elev);
            //    //level.Name = $"{DateTime.Today.ToString("yyyyMMdd")}_Абс.отм.";

            //    //foreach (var link in doc.ToElements<RevitLinkInstance>())
            //    //{
            //    //    var zCoord = link.GetTotalTransform().Origin.Z;

            //    //    ElementTransformUtils.MoveElement(doc, link.Id, new XYZ(0, 0, elev - zCoord));
            //    //}

            //    foreach (var elem in RevitAPI.UIDocument.ToSelectedElements())
            //    {
            //        elem.LookupParameter("OLP_Task_Высота").Set(720 * intUnit);
            //        elem.LookupParameter("OLP_Task_Глубина").Set(1320 * intUnit);
            //        elem.LookupParameter("OLP_Task_Ширина").Set(2160 * intUnit);
            //    }



            //    t.Commit();
            //}

            ProjectLocation currentLocation = doc.ActiveProjectLocation;

            XYZ origin = new XYZ();
            ProjectPosition projectPosition = currentLocation.GetProjectPosition(origin);

            double elevation = UnitUtils.ConvertToInternalUnits(130, ParameterMethods.GetUnitType("m"));

            ProjectPosition newPosition =
              doc.Application.Create.NewProjectPosition(projectPosition.EastWest, projectPosition.NorthSouth, elevation, projectPosition.Angle);

            using (Transaction t = new Transaction(doc, "Задать абсолютную отметку"))
            {
                t.Start();

                if (newPosition != null)
                {
                    currentLocation.SetProjectPosition(origin, newPosition);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}