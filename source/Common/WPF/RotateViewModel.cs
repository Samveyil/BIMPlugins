using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Bars;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;

namespace BIMPlugins.Common.WPF
{
    public partial class RotateViewModel : ObservableObject
    {
        [ObservableProperty] private string _count = "0";
        [ObservableProperty] private double _angle = 90;

        private List<Element> _elements = [];
        private ExternalEvent ExEvent { get; }

        public RotateViewModel()
        {
            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.RotateElements());

            _elements = RevitAPI.UIDocument.ToSelectedElements()
                .Where(e => e.Category.CategoryType == CategoryType.Model && e.Location is LocationPoint)
                .ToList();
            Count = _elements.Count.ToString();
        }

        [RelayCommand]
        private void SelectElems()
        {
            RevitOptionsBar.Hide(true);
            try
            {
                _elements = RevitAPI.UIDocument.PickObjects(new ModelElementsFilter(), "Выберите элементы").ToList();
                Count = _elements.Count.ToString();
            }
            catch { }
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
                using (Transaction t = new Transaction(RevitAPI.Document, "Поворот элемента вокруг своей оси"))
                {
                    t.Start();

                    foreach (var element in _elements)
                    {
                        XYZ axis = new XYZ(0, 0, 1);

                        LocationPoint locationPoint = element.Location as LocationPoint;
                        XYZ rotationPoint = locationPoint.Point;

                        Line line = Line.CreateUnbound(rotationPoint, axis);

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