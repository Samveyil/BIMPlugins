using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CloseDocsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            foreach (Document doc in RevitAPI.Application.Documents)
            {
                if (doc.IsLinked == true) { continue; }
                
                UIDocument uiDocument = new UIDocument(doc);
                if (uiDocument.GetOpenUIViews().Count == 0)
                {
                    doc.Close(false);
                }
            }

            return Result.Succeeded;
        }
    }
}