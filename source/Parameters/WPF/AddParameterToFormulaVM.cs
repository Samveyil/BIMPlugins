using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;

namespace BIMPlugins.Parameters.WPF
{
    public partial class AddParameterToFormulaVM : ObservableObject
    {
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _selectedParameter;
        [ObservableProperty] private string _filter;

        partial void OnFilterChanged(string value) => Parameters.Refresh();

        public ICollectionView Parameters { get; }
        public bool UserSelect { get; private set; }

        public AddParameterToFormulaVM(List<Parameter> parameters)
        {
            Parameters = CollectionViewSource.GetDefaultView(parameters.ToArray());
            Parameters.Filter = item => string.IsNullOrEmpty(Filter)
                || ((Parameter)item).Definition.Name.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            UserSelect = true;
            RaiseCloseRequest();
        }

        private bool CanRun() => SelectedParameter != null;

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
