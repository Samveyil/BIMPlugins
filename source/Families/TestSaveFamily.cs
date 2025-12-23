using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.IO;
using System.Linq;

namespace BIMPlugins.Families
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TestSaveFamily : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //foreach (var filePath in Directory.GetFiles(@"\\Diskstation\производство\Ревит\FAMILIES\2019\ОСК\280_2D_Арматура"))
            //{
            //    var doc = RevitAPI.Application.OpenDocumentFile(filePath);
            //    var famManager = doc.FamilyManager;

            //    var nameParameter = famManager.get_Parameter("ADSK_Наименование");

            //    using (Transaction t = new Transaction(doc, "Очистить"))
            //    {
            //        t.Start();

            //        //var linePatterns = doc.ToElements<LinePatternElement>().Where(p => p.Name != "Линия выравнивания").Select(p => p.Id).ToList();
            //        //var fillPatterns = doc.ToElements<FillPatternElement>().Where(p => p.Name != "<Сплошная заливка>").Select(p => p.Id).ToList();

            //        //doc.Delete(fillPatterns);
            //        //doc.Delete(linePatterns);

            //        famManager.MakeInstance(nameParameter);
            //        famManager.SetFormula(nameParameter, nameParameter.Formula.Replace("проката", "проката, ADSK_Диаметр арматуры"));


            //        t.Commit();
            //    }

            //    var saveOpt = new SaveAsOptions()
            //    {
            //        MaximumBackups = 1
            //    };

            //    var newPath = filePath.Replace(".rfa", "1.rfa");
            //    doc.SaveAs(newPath, saveOpt);

            //    doc.Close(false);

            //    File.Delete(filePath);
            //    File.Move(newPath, filePath);
            //}

            var doc = RevitAPI.Document;
            var famManager = doc.FamilyManager;

            //var nameParameter = famManager.get_Parameter("ADSK_Наименование");

            //using (Transaction t = new Transaction(doc, "Очистить"))
            //{
            //    t.Start();

            //    var linePatterns = doc.ToElements<LinePatternElement>().Where(p => p.Name != "Линия выравнивания").Select(p => p.Id).ToList();
            //    var fillPatterns = doc.ToElements<FillPatternElement>().Where(p => p.Name != "<Сплошная заливка>").Select(p => p.Id).ToList();

            //    doc.Delete(fillPatterns);
            //    doc.Delete(linePatterns);

            //    //famManager.MakeInstance(nameParameter);
            //    //famManager.SetFormula(nameParameter, @"size_lookup(ТВ, ""Наименование"", ""Не найдено"", ADSK_Код металлопроката, ADSK_Диаметр арматуры)");

            //    t.Commit();
            //}

            var path = doc.PathName;

            var saveOpt = new SaveAsOptions()
            {
                PreviewViewId = RevitAPI.ActiveView.Id,
                MaximumBackups = 1
            };

            var newPath = path.Replace(".rfa", "1.rfa");
            doc.SaveAs(newPath, saveOpt);

            RevitAPI.UIApplication.OpenAndActivateDocument(@$"C:\ProgramData\Autodesk\RVT {RevitAPI.Application.VersionNumber}\Templates\Generic\Default_M_RUS.rte");
            doc.Close(false);

            File.Delete(path);
            File.Move(newPath, path);

            return Result.Succeeded;
        }
    }
}