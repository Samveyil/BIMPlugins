using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;

namespace BIMPlugins.Parameters.WPF
{
    public partial class AddParameterToFormulaViewModel(List<Parameter> parameters) : ObservableObject
    {
        [ObservableProperty] private Parameter _selectedParameter;

        public List<Parameter> Parameters { get; } = parameters;
        public bool UserSelect { get; private set; } = false;

        [RelayCommand]
        private void Run()
        {
            UserSelect = true;
            RaiseCloseRequest();
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}