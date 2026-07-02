using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;

#if DEBUG
namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TemplateVersionCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            using (Transaction t = new Transaction(RevitAPI.Document, "Изменить версию шаблона"))
            {
                t.Start();

                RevitAPI.Document.ProjectInformation.ToParameter("OLP_Версия шаблона").Set("1.2.28");

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
#endif