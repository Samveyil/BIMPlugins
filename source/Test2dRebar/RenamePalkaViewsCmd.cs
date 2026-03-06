using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Test2dRebar.Classes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Test2dRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RenamePalkaViewsCmd : IExternalCommand
    {
        private Guid _idGuid = RebarMethods.IdGuid;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var idParamId = doc.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == _idGuid).Id;

            var palkas = doc.ToElements<FamilyInstance>(BuiltInCategory.OST_DetailComponents)
                .Where(r => r.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString().StartsWith("285") && !r.get_Parameter(_idGuid).AsString().IsNullOrEmpty())
                .GroupBy(p => p.get_Parameter(_idGuid).AsString())
                .Select(g => g.First())
                .ToList();

            var views = new List<View>();

            using (Transaction t = new Transaction(doc, "Переименовать виды"))
            {
                t.Start();

                foreach (var palka in palkas)
                {
                    var razdel = palka.get_Parameter(RebarMethods.RazdelGuid).AsString();
                    var palkaNumber = palka.get_Parameter(RebarMethods.NumberGuid).AsString();
                    if (palkaNumber.IsNullOrEmpty())
                        continue;

                    var wallMark = RebarMethods.GetWallMark(palka, palkaNumber);

                    var param = palka.get_Parameter(_idGuid).AsString();
                    foreach (var strId in param.Split(';').Select(s => s.Trim()).ToList())
                    {
                        var view = new ElementId(int.Parse(strId)).ToElement<View>();

                        SetViewName(view, $"21_{razdel}_{wallMark}_" + view.Name.Split('_').LastOrDefault());
                    }
                }

                foreach (var palka in palkas)
                {
                    var param = palka.get_Parameter(_idGuid).AsString();
                    foreach (var strId in param.Split(';').Select(s => s.Trim()).ToList())
                    {
                        var view = new ElementId(int.Parse(strId)).ToElement<View>();

                        try
                        {
                            view.Name = view.Name.TrimStart('$');
                        }
                        catch { }
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }

        private void SetViewName(View view, string baseName)
        {
            string currentName = baseName;
            int counter = 0;

            while (true)
            {
                try
                {
                    view.Name = currentName;
                    break;
                }
                catch (Exception)
                {
                    counter++;

                    currentName = new string('$', counter) + baseName;

                    if (counter > 20)
                    {
                        throw new Exception("Не удалось найти свободное имя после 10 попыток.");
                    }
                }
            }
        }
    }
}