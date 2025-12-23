using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.Windows;

namespace BIMPlugins.Views
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ActivateByIdCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (int.TryParse(Clipboard.GetText(), out int id))
            {
                var element = new ElementId(id).ToElement();
                if (element is View view)
                {
                    RevitAPI.UIDocument.ActiveView = view;
                }
                else
                {
                    if (element.OwnerViewId != null)
                    {
                        RevitAPI.UIDocument.ActiveView = element.OwnerViewId.ToElement<View>();
                        RevitAPI.UIDocument.Selection.SetElementIds([element.Id]);
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