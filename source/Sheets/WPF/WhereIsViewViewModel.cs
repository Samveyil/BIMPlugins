using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Sheets.WPF
{
    public partial class WhereIsViewViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<ViewSheetItem> _viewSheets = [];

        public WhereIsViewViewModel(List<ViewSheet> viewSheets)
        {
            foreach (var viewSheet in viewSheets)
            {
                ViewSheets.Add(new ViewSheetItem(viewSheet));
            }

            ViewSheets = new(ViewSheets.OrderBy(v => v.Title).ToList());
        }
    }

    public partial class ViewSheetItem(ViewSheet viewSheet)
    {
        public string Title { get; set; } = viewSheet.Title;
        public ElementId Id { get; set; } = viewSheet.Id;

        [RelayCommand]
        private void OpenSheet()
        {
            RevitAPI.UIDocument.ActiveView = Id.ToElement<ViewSheet>();

            var uiView = RevitAPI.UIDocument.GetOpenUIViews().FirstOrDefault(v => v.ViewId.ToString() == Id.ToString());
            uiView.ZoomToFit();
        }
    }
}