using Autodesk.Revit.DB;
using System.Windows;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Common.WPF
{
    public partial class MirrorViewModel : ObservableObject
    {
        [ObservableProperty] private bool _currentView = true;

        private ExternalEvent ExEvent { get; set; }

        public MirrorViewModel()
        {
            var handler = new RevitAPI.MyEventHandler<MirrorViewModel>(
                this,
                vm => vm.FindMirror()
            );
            ExEvent = ExternalEvent.Create(handler);
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

                ICollection<ElementId> mirroredElems = collector
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(f => f.Mirrored)
                    .Select(f => f.Id)
                    .ToList();

                if (mirroredElems.Count > 0)
                {
                    RevitAPI.UIDocument.Selection.SetElementIds(mirroredElems);
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
