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
                if (new ElementId(id).ToElement() is View view)
                {
                    RevitAPI.UIDocument.ActiveView = view;
                }
                else
                {
                    message = "Элемент не является видом";
                    return Result.Failed;
                }
            }
            else
            {
                message = "В буфере обмена не содержится Id вида";
                return Result.Failed;
            }

            return Result.Succeeded;
        }
    }
}