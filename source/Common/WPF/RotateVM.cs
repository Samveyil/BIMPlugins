using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class RotateVM : ObservableObject
    {
        [ObservableProperty] private int _count;
        [ObservableProperty] private double _angle = 90;

        private readonly UIDocument _uiDoc;
        private List<Element> _elements;

        private ExternalEvent ExEvent { get; }

        public RotateVM()
        {
            _uiDoc = RevitAPI.UIDocument;

            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.RotateElements());

            _elements = _uiDoc.ToSelectedElements()
                .Where(IsRotatable)
                .ToList();
            Count = _elements.Count;
        }

        [RelayCommand]
        private void SelectElems()
        {
            RevitOptionsBar.Hide(true);
            try
            {
                var elements = _uiDoc.PickElements(new ModelElementsFilter(), "Выберите элементы");
                if (elements != null)
                {
                    _elements = elements.ToList();
                    Count = _elements.Count;
                }
            }
            finally
            {
                RevitOptionsBar.Show();
            }
        }

        [RelayCommand]
        private void Run() => ExEvent.Raise();

        private void RotateElements()
        {
            try
            {
                var angle = Angle.FromDegrees();

                using (Transaction t = new Transaction(_uiDoc.Document, "Поворот элемента вокруг своей оси"))
                {
                    t.Start();

                    foreach (var element in _elements)
                        element.Rotate(Line.CreateUnbound(element.ToPoint(), XYZ.BasisZ), angle);

                    t.Commit();
                }
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

        private static bool IsRotatable(Element element) =>
            element.Category?.CategoryType == CategoryType.Model && element.Location is LocationPoint;

        private class ModelElementsFilter : ISelectionFilter
        {
            public bool AllowElement(Element element) => IsRotatable(element);

            public bool AllowReference(Reference reference, XYZ position) => true;
        }
    }
}
