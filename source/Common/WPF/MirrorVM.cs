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
using System.Windows;

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
                    MessageWindow.ShowMessage("Не было найдено отзеркаленных элементов", "Зеркало", icon: MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageWindow.ShowMessage($"{ex.Message}\n{ex.StackTrace}", "Ошибка", icon: MessageBoxImage.Error);
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
