using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Common.WPF
{
    public partial class MirrorViewModel : ObservableObject
    {
        [ObservableProperty] private bool _currentView = true;

        private ExternalEvent ExEvent { get; }

        public MirrorViewModel()
        {
            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.FindMirror());
        }

        [RelayCommand]
        private void ShowMirroredElements() => ExEvent.Raise();
        
        private void FindMirror()
        {
            try
            {
                var doc = RevitAPI.Document;

                var collector = CurrentView
                ? new FilteredElementCollector(doc, doc.ActiveView.Id)
                : new FilteredElementCollector(doc);

                var mirroredElems = collector
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(f => f.Mirrored)
                    .Select(f => f.Id)
                    .ToList();

                if (mirroredElems.Any())
                {
                    new UIDocument(doc).Selection.SetElementIds(mirroredElems);
                }
                else
                {
                    TaskDialog.Show("Отработка скрипта", "Не было найдено отзеркаленных элементов");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}\n{ex.StackTrace}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RevitOptionsBar.Hide();
            }
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}