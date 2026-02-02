using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Linq;

namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AddNewPalkaCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            Guid _idGuid = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");

            var palka = RevitAPI.UIDocument.ToSelectedElements().FirstOrDefault();
            if (palka == null)
                return Result.Cancelled;

            var view = new ElementId(25747091).ToElement();
            var idParam = view.get_Parameter(_idGuid);

            var filter = idParam.Id.CreateEqualsFilter(idParam.AsString());
            var elems = doc.ToElements(filter).ToList();

            var newValue = string.Join(";", [idParam.AsString(), palka.Id.ToString()]);

            using (Transaction t = new Transaction(doc, "test"))
            {
                t.Start();

                foreach (var element in elems)
                {
                    var param = element.get_Parameter(_idGuid);

                    if (!param.IsReadOnly)
                        param.Set(newValue);
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}