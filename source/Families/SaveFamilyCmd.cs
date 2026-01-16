using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using System.IO;

namespace BIMPlugins.Families
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SaveFamilyCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;
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