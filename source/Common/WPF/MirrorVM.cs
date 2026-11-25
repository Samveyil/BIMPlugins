using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class MirrorVM : ObservableObject
    {
        [ObservableProperty] private bool _currentView = true;

        [RelayCommand]
        private void ShowMirroredElements()
        {
            try
            {
                var uiDoc = RevitAPI.UIDocument;
                var doc = uiDoc.Document;

                var mirroredElems = (CurrentView ? doc.ToElements<FamilyInstance>(doc.ActiveView.Id) : doc.ToElements<FamilyInstance>())
                    .Where(f => f.Mirrored)
                    .Select(f => f.Id)
                    .ToList();

                if (mirroredElems.Count != 0)
                    uiDoc.Selection.SetElementIds(mirroredElems);
                else
                    MessageDialog.Show("Не было найдено отзеркаленных элементов", "Зеркало", icon: MessageIcon.Information);
            }
            catch (Exception ex)
            {
                MessageDialog.Show($"{ex.Message}\n{ex.StackTrace}", "Ошибка", icon: MessageIcon.Error);
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
