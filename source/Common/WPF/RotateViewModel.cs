using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Common.WPF
{
    public partial class RotateViewModel : ObservableObject
    {
        [ObservableProperty] private string _count = "0";
        [ObservableProperty] private double _angle = 90;

        private readonly UIDocument _uiDoc;
        private List<Element> _elements = [];

        private ExternalEvent ExEvent { get; }

        public RotateViewModel()
        {
            _uiDoc = RevitAPI.UIDocument;

            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.RotateElements());

            _elements = _uiDoc.ToSelectedElements()
                .Where(e => e.Category.CategoryType == CategoryType.Model && e.Location is LocationPoint)
                .ToList();
            Count = _elements.Count.ToString();
        }

        [RelayCommand]
        private void SelectElems()
        {
            RevitOptionsBar.Hide(true);

            _elements = _uiDoc.PickElements(new ModelElementsFilter(), "Выберите элементы").ToList();
            Count = _elements?.Count.ToString();

            RevitOptionsBar.Show();
        }

        [RelayCommand]
        private void Run() => ExEvent.Raise();

        private void RotateElements()
        {
            try
            {
                using (Transaction t = new Transaction(_uiDoc.Document, "Поворот элемента вокруг своей оси"))
                {
                    t.Start();

                    foreach (var element in _elements)
                    {
                        Line line = Line.CreateUnbound(element.ToPoint(), XYZ.BasisZ);
                        double angleInRadians = Angle * (Math.PI / 180);

                        element.Rotate(line, angleInRadians);
                    }

                    t.Commit();
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

        private class ModelElementsFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)
            {
                return (element.Category.CategoryType == CategoryType.Model && element.Location is LocationPoint);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
    }
}