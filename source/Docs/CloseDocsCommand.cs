using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.MessageBoxes;
using System.Collections.Generic;

namespace BIMPlugins.Docs
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CloseDocsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var toShow = new List<object>();
            foreach (Document doc in RevitAPI.Application.Documents)
            {
                if (doc.IsLinked == true) { continue; }
                
                UIDocument uiDocument = new UIDocument(doc);
                if (uiDocument.GetOpenUIViews().Count == 0)
                {
                    toShow.Add(doc.Title);
                    doc.Close(false);
                }
            }

            if (toShow.Count > 0)
            {
                toShow.Insert(0, "Закрытые документы:");

                var reportWindow = new ReportWindow(toShow);
                reportWindow.ShowDialog();
            }

            return Result.Succeeded;
        }
    }
}