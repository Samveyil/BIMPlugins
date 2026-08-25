using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Views
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ActivateByIdCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiDoc = RevitAPI.UIDocument;

            var selectedElem = uiDoc.ToSelectedElements().FirstOrDefault();
            if (selectedElem != null && selectedElem.OwnerViewId != ElementId.InvalidElementId)
            {
                var ownerView = selectedElem.OwnerViewId.ToElement<View>();
                uiDoc.ActiveView = ownerView;
                ownerView.ToUIView()?.ZoomToFit();
            }
            else if (int.TryParse(Clipboard.GetText(), out int id))
            {
                var element = new ElementId(id).ToElement();
                if (element is View view)
                {
                    uiDoc.ActiveView = view;
                    view.ToUIView()?.ZoomToFit();
                }
                else
                {
                    if (element.OwnerViewId != ElementId.InvalidElementId)
                    {
                        var ownerView = element.OwnerViewId.ToElement<View>();
                        uiDoc.ActiveView = ownerView;
                        ownerView.ToUIView()?.ZoomToFit();
                        
                        uiDoc.Selection.SetElementIds([element.Id]);
                    }
                    else
                    {
                        message = "Невозможно определить вид!";
                        return Result.Failed;
                    }
                }
            }
            else
            {
                message = "В буфере обмена не содержится Id";
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}