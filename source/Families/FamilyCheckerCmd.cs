using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Families.WPF;
using System.Linq;

namespace BIMPlugins.Families
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FamilyCheckerCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var famInstance = RevitAPI.UIDocument
                .ToSelectedElements()
                .FirstOrDefault(el => el is FamilyInstance);

            if (famInstance == null)
            {
                message = "Выберите экземпляр семейства!";
                return Result.Cancelled;
            }

            var viewModel = new FamilyCheckerVM(famInstance as FamilyInstance);
            var window = new FamilyCheckerWindow(viewModel);

            window.Show();

            return Result.Succeeded;
        }
    }
}