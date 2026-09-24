using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using BIMPlugins.ExtStorage.Extensions;

namespace BIMPlugins.Parameters.WPF
{
    public partial class NumerateViewModel : ObservableObject
    {
        [ObservableProperty] private string _prefix;
        [ObservableProperty] private double _number = 1;
        [ObservableProperty] private bool _isNotNumbering = true;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _selectedParameter;

        private readonly UIDocument _uiDoc;
        private readonly Document _doc;

        public List<Parameter> Parameters { get; set; } = [];
        
        private ExternalEvent ExEvent { get; }

        public NumerateViewModel()
        {
            _uiDoc = RevitAPI.UIDocument;
            _doc = _uiDoc.Document;

            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.Numerate());

            var element = _uiDoc.PickElement("Выберите элемент");
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
            using (TransactionGroup tGroup = new TransactionGroup(_doc, "Нумеровать элементы"))
            {
                tGroup.Start();
                
                while (true)
                {
                    IsNotNumbering = false;

                    using (Transaction t = new Transaction(_doc, "Нумерация элеметов"))
                    {
                        t.Start();

                        try
                        {
                            var element = _uiDoc.PickElement($"Выберите {Number} элемент. Нажмите Esc для завершения нумерации!");
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