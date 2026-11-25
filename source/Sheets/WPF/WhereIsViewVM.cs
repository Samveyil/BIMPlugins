using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.Classes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Sheets.WPF
{
    public partial class WhereIsViewVM(List<ViewSheet> viewSheets) : ObservableObject
    {
        private readonly UIDocument _uiDoc = RevitAPI.UIDocument;

        public List<SheetItem> ViewSheets { get; } = viewSheets
            .Select(s => new SheetItem(s))
            .OrderBy(s => s.Title, NaturalComparer.CurrentCultureIgnoreCase)
            .ToList();

        [RelayCommand]
        private void OpenSheet(SheetItem item)
        {
            _uiDoc.ActiveView = item.Element;
            item.Element.ToUIView()?.ZoomToFit();
        }
    }
}
