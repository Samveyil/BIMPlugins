using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Sheets.Classes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BIMPlugins.Sheets.WPF
{
    public partial class WhereIsViewViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<SheetItem> _viewSheets = [];

        private readonly UIDocument _uiDoc;

        public WhereIsViewViewModel(List<ViewSheet> viewSheets)
        {
            _uiDoc = RevitAPI.UIDocument;

            foreach (var viewSheet in viewSheets)
            {
                ViewSheets.Add(new SheetItem(viewSheet));
            }

            ViewSheets = new(ViewSheets.OrderBy(v => v.Title).ToList());
        }

        [RelayCommand]
        private void OpenSheet(SheetItem item)
        {
            var viewSheet = item.Element;

            _uiDoc.ActiveView = viewSheet;
            viewSheet.ToUIView()?.ZoomToFit();
        }
    }
}