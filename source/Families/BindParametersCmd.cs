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
    public class BindParametersCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var selectedInstance = RevitAPI.UIDocument.ToSelectedElements().FirstOrDefault();
            if (selectedInstance is not FamilyInstance instance)
            {
                selectedInstance = RevitAPI.UIDocument.PickObject<FamilyInstance>("Выберите экземпляр семейства");
                if (selectedInstance == null) { return Result.Cancelled; }
            }

            var viewModel = new BindParametersViewModel((FamilyInstance)selectedInstance);
            var window = new BindParametersWindow(viewModel);
            viewModel.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}