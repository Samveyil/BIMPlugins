using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Test2dRebar.Classes;
using System;
using System.Linq;

namespace BIMPlugins.Test2dRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdatePalkaCmd : IExternalCommand
    {
        private Guid _idGuid = RebarMethods.IdGuid;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var idParamId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == _idGuid).Id;

            View view;
            var selectedPalka = RevitAPI.UIDocument.ToSelectedElements().FirstOrDefault(e => e is FamilyInstance instance && instance.Symbol.FamilyName.Contains("Палка"));
            if (selectedPalka != null)
            {
                var viewId = selectedPalka.get_Parameter(_idGuid).AsString()?.Split(';')[0];
                if (viewId.IsNullOrEmpty())
                {
                    message = "Не возможно определить вид, относящийся к выбранной палке"!;
                    return Result.Cancelled;
                }

                view = new ElementId(int.Parse(viewId)).ToElement<View>();
            }
            else
                view = doc.ActiveView;

            var ids = view.get_Parameter(_idGuid).AsString();
            if (ids.IsNullOrEmpty())
            {
                message = "Не возможно определить палки, относящиеся к активному виду"!;
                return Result.Cancelled;
            }

            RebarMethods.UpdateElements(doc, idParamId, view);

            return Result.Succeeded;
        }
    }
}