using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;

namespace BIMPlugins.Sheets.Classes
{
    public partial class SheetCopyItem(ViewSheet viewSheet) : SheetItem(viewSheet)
    {
        [ObservableProperty] private string _newCopyName;
        [ObservableProperty] private int _copiesAmount = 1;

        public List<ViewItem> ViewItems { get; set; } = [];


        [RelayCommand]
        private void Plus()
        {
            CopiesAmount++;
        }

        [RelayCommand]
        private void Minus()
        {
            CopiesAmount--;
            if (CopiesAmount < 1)
                CopiesAmount = 1;
        }
    }
}