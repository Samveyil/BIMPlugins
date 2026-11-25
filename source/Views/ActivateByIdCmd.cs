using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Views
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ActivateByIdCmd : IExternalCommand
    {
        private const string Title = "Перейти по Id";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;
            var doc = uiDoc.Document;

            var selectedElem = uiDoc.ToSelectedElements().FirstOrDefault();
            if (selectedElem != null && selectedElem.OwnerViewId != ElementId.InvalidElementId)
            {
                Activate(uiDoc, selectedElem.OwnerViewId.ToElement<View>(doc));
                return Result.Succeeded;
            }

            var element = ParseId(Clipboard.GetText())?.ToElement(doc);
            if (element == null)
            {
                MessageWindow.ShowMessage("В буфере обмена нет Id элемента этого проекта!", Title, icon: MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            if (element is View view)
            {
                Activate(uiDoc, view);
                return Result.Succeeded;
            }

            if (element.OwnerViewId == ElementId.InvalidElementId)
            {
                MessageWindow.ShowMessage("Элемент не принадлежит ни одному виду!", Title, icon: MessageBoxImage.Warning);
                return Result.Cancelled;
            }

            Activate(uiDoc, element.OwnerViewId.ToElement<View>(doc));
            uiDoc.Selection.SetElementIds([element.Id]);

            return Result.Succeeded;
        }

        private static void Activate(UIDocument uiDoc, View view)
        {
            uiDoc.ActiveView = view;
            view.ToUIView()?.ZoomToFit();
        }

        // Ids are long since Revit 2024; the text may come with spaces or a line break around it.
        private static ElementId ParseId(string text)
        {
#if R2024_OR_GREATER
            return long.TryParse(text?.Trim(), out var id) ? new ElementId(id) : null;
#else
            return int.TryParse(text?.Trim(), out var id) ? new ElementId(id) : null;
#endif
        }
    }
}
