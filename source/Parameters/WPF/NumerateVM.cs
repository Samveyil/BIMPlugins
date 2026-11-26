using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Parameters.WPF
{
    public partial class NumerateVM : ObservableObject
    {
        public const string Title = "Нумерация элементов";

        [ObservableProperty] private string _prefix;
        [ObservableProperty] private int _number = 1;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private bool _isNotNumbering = true;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _selectedParameter;

        private readonly UIDocument _uiDoc;
        private readonly ExternalEvent _exEvent;

        public List<Parameter> Parameters { get; }

        public NumerateVM(UIDocument uiDoc, Element element)
        {
            _uiDoc = uiDoc;
            _exEvent = RevitAPI.CreateExtEvent(this, vm => vm.Numerate());

            Parameters = element.Parameters
                .Cast<Parameter>()
                .Where(p => p.StorageType == StorageType.String && !p.IsReadOnly)
                .OrderBy(p => p.Definition.Name)
                .ToList();
        }

        // The bar is locked at once, so that a second click does not queue a second numbering.
        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            IsNotNumbering = false;
            _exEvent.Raise();
        }

        private bool CanRun() => SelectedParameter != null && IsNotNumbering;

        private void Numerate()
        {
            var doc = _uiDoc.Document;
            var parameterName = SelectedParameter.Definition.Name;

            try
            {
                using (TransactionGroup tGroup = new TransactionGroup(doc, Title))
                {
                    tGroup.Start();

                    while (true)
                    {
                        var element = _uiDoc.PickElement($"Выберите элемент с номером {Prefix}{Number}. Esc — завершить нумерацию");
                        if (element == null)
                            break;

                        var parameter = element.ToParameter(SelectedParameter.Id);
                        if (parameter == null || parameter.IsReadOnly)
                        {
                            MessageDialog.Show($"У элемента «{element.Name}» нет параметра «{parameterName}» или его нельзя изменить. Выберите другой элемент.",
                                Title, icon: MessageIcon.Warning);
                            continue;
                        }

                        using (Transaction t = new Transaction(doc, Title))
                        {
                            t.Start();
                            parameter.Set(Prefix + Number);
                            t.Commit();
                        }

                        Number++;
                    }

                    tGroup.Assimilate();
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException ex)
            {
                MessageDialog.Show($"Нумерация остановлена: {ex.Message}", Title, icon: MessageIcon.Error);
            }
            finally
            {
                IsNotNumbering = true;
            }
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}
