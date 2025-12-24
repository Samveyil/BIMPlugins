using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage.Methods;
using System.Collections.Generic;
using System.Linq;
using BIMPlugins.ExtStorage.Extensions;

namespace BIMPlugins.Common.WPF
{
    public partial class NumerateViewModel : ObservableObject
    {
        [ObservableProperty] private string _prefix;
        [ObservableProperty] private double _number = 1;
        [ObservableProperty] private bool _isNotNumbering = true;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _selectedParameter;

        public List<Parameter> Parameters { get; set; } = [];
        
        private ExternalEvent ExEvent { get; set; }

        public NumerateViewModel()
        {
            var handler = new RevitAPI.MyEventHandler<NumerateViewModel>(
                this,
                vm => vm.Numerate()
            );
            ExEvent = ExternalEvent.Create(handler);

            var element = RevitAPI.UIDocument.PickObject("Выберите элемент");
            if (element == null) return;

            Parameters = element.Parameters
                .Cast<Parameter>()
                .Where(p => p.StorageType == StorageType.String && !p.IsReadOnly)
                .OrderBy(p => p.Definition.Name)
                .ToList();
        }

        [RelayCommand(CanExecute = nameof(IsEnabled))]
        private void Run() => ExEvent.Raise();
        private void Numerate()
        {
            using (TransactionGroup tGroup = new TransactionGroup(RevitAPI.Document, "Нумеровать элементы"))
            {
                tGroup.Start();
                
                while (true)
                {
                    IsNotNumbering = false;

                    using (Transaction t = new Transaction(RevitAPI.Document, "Нумерация элеметов"))
                    {
                        t.Start();

                        try
                        {
                            var element = RevitAPI.UIDocument.PickObject($"Выберите {Number} элемент. Нажмите Esc для завершения нумерации!");
                            if (element == null) break;

                            var parameter = element.LookupParameter(SelectedParameter.Definition.Name);

                            if (parameter != null)
                            {
                                string value = Prefix + Number.ToString();
                                parameter.Set(value);

                                Number++;
                            }
                            else
                            {
                                TaskDialog.Show("Ошибка", $"У элемента {element.Name} нет параметра {SelectedParameter}");
                                break;
                            }
                        }
                        catch { break; }
                        finally { t.Commit(); }
                    }
                }

                IsNotNumbering = true;

                tGroup.Assimilate();
            }
        }

        private bool IsEnabled()
        {
            return SelectedParameter != null;
        }


        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}