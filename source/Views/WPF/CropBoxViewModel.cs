using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.Methods;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Views.WPF
{
    public partial class CropBoxViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<ViewCropBoxItem> _viewCropBoxItems = [];
        [ObservableProperty] private static bool _showSectionPlane = true;
        [ObservableProperty] private string _filter;
        [ObservableProperty] private ICollectionView _filteredItems;

        private ExternalEvent DeleteSectionPlaneExEvent { get; }
        public ExternalEvent DeleteDirectShapesExEvent { get; }

        partial void OnFilterChanged(string value) => FilteredItems.Refresh();
        partial void OnShowSectionPlaneChanging(bool value)
        {
            foreach (var viewCropBox in ViewCropBoxItems.Where(v => v.IsSelected).ToList())
            {
                if (value)
                {
                    viewCropBox.SectionPlaneExEvent.Raise();
                }
                else
                {
                    DeleteSectionPlaneExEvent.Raise();
                }
            }
        }

        private readonly Document _doc = RevitAPI.Document;
        private const double GoldenRatioConjugate = 0.618033988749895;
        private static double _currentHue = 0;

        private static ElementId _patternId;
        private static View3D _view3D = null;

        private bool FilterViews(object obj)
        {
            if (obj is ViewCropBoxItem viewCropBoxItem)
            {
                return Filter.IsNullOrEmpty() || viewCropBoxItem.ViewName.ToLower().Contains(Filter.ToLower());
            }
            return false;
        }


        public CropBoxViewModel()
        {
            DeleteSectionPlaneExEvent = RevitAPI.CreateExtEvent(this, vm => vm.DeletePlanes());
            DeleteDirectShapesExEvent = RevitAPI.CreateExtEvent(this, vm => vm.DeleteAllDirectShapes());

            _view3D = GetView3D();

            _patternId = _doc.ToElements<FillPatternElement>()
                .FirstOrDefault(p => p.Name == "<Сплошная заливка>")
                .Id;

            var filter = new ElementMulticlassFilter(new List<Type> { typeof(ViewPlan), typeof(ViewSection)});
            var views = _doc.ToElements<View>(filter)
                .Where(v => !v.IsTemplate)
                .OrderBy(x => x.Title)
                .ToList();

            foreach (var view in views)
            {
                ViewCropBoxItems.Add(new ViewCropBoxItem(view, GenerateDistinctColor()));
            }

            FilteredItems = CollectionViewSource.GetDefaultView(ViewCropBoxItems);
            FilteredItems.Filter = FilterViews;
        }

        private void DeletePlanes()
        {
            using (Transaction t = new Transaction(_doc, "Скрыть секущую плоскость диапазона видимости"))
            {
                t.Start();

                foreach (var viewCropBox in ViewCropBoxItems.Where(v => v.SectionPlaneId != null).ToList())
                {
                    _doc.Delete(viewCropBox.SectionPlaneId);
                    viewCropBox.SectionPlaneId = null;
                }

                t.Commit();
            }
        }
        private void DeleteAllDirectShapes()
        {
            using (Transaction t = new Transaction(_doc, "Удаление всех диапазонов видимости"))
            {
                t.Start();

                foreach (var viewCropBox in ViewCropBoxItems.Where(v => v.IsSelected).ToList())
                {
                    _doc.Delete(viewCropBox.DirectShapeId);

                    if (viewCropBox.SectionPlaneId != null)
                        _doc.Delete(viewCropBox.SectionPlaneId);
                }

                t.Commit();
            }
        }


        private View3D GetView3D()
        {
            if (_doc.ActiveView is View3D view3D)
            {
                return view3D;
            }
            else
            {
                var viewName = _doc.IsWorkshared
                    ? "{3D - " + RevitAPI.Application.Username + "}"
                    : "{3D}";

                return _doc.GetView3D(viewName);
            }
        }

        private Color GenerateDistinctColor()
        {
            _currentHue += GoldenRatioConjugate;
            _currentHue %= 1.0;

            double hue = _currentHue * 360;

            double saturation = 0.7;
            double value = 0.9;

            double c = value * saturation;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = value - c;

            double r, g, b;

            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return new Color(
                (byte)((r + m) * 255),
                (byte)((g + m) * 255),
                (byte)((b + m) * 255)
            );
        }

        public partial class ViewCropBoxItem : ObservableObject
        {
            [ObservableProperty] private string _viewName;
            [ObservableProperty] private bool _isSelected = false;
            [ObservableProperty] private Color _color;
            [ObservableProperty] private View _view;

            private readonly UIDocument _uiDoc;
            private readonly Document _doc;

            public ExternalEvent SectionPlaneExEvent { get; }

            public ElementId DirectShapeId { get; set; }
            public ElementId SectionPlaneId { get; set; }

            private ExternalEvent CreateExEvent { get;}
            private ExternalEvent DeleteExEvent { get; }
            private ExternalEvent SectBoxExEvent { get; }

            partial void OnIsSelectedChanging(bool value)
            {
                _uiDoc.ActiveView = _view3D;

                if (value)
                {
                    CreateExEvent.Raise();
                }
                else
                {
                    DeleteExEvent.Raise();
                }
            }


            public ViewCropBoxItem(View view, Color color)
            {
                _doc = view.Document;
                _uiDoc = new UIDocument(_doc);

                CreateExEvent = RevitAPI.CreateExtEvent(this, vm => vm.CreateDS());
                DeleteExEvent = RevitAPI.CreateExtEvent(this, vm => vm.DeleteDS());

                SectBoxExEvent = RevitAPI.CreateExtEvent(this, vm => vm.SectBox());
                SectionPlaneExEvent = RevitAPI.CreateExtEvent(this, vm => vm.SectionPlane());

                View = view;
                ViewName = view.Title.Replace("_", "__");
                Color = color;
            }


            [RelayCommand]
            private void OpenView()
            {
                _uiDoc.ActiveView = View;
                View.ToUIView()?.ZoomToFit();
            }

            [RelayCommand]
            private void SectionBox() => SectBoxExEvent.Raise();

            [RelayCommand]
            private void Select() => _uiDoc.Selection.SetElementIds([DirectShapeId]);

            private void CreateDS()
            {
                var minPoint = new XYZ();
                var maxPoint = new XYZ();

                using (TransactionGroup tGroup = new TransactionGroup(_doc, "Создание диапазона видимости"))
                {
                    tGroup.Start();

                    using (Transaction t = new Transaction(_doc, "Создание диапазона видимости"))
                    {
                        t.Start();

                        if (View is ViewPlan viewPlan)
                        {
                            var viewRange = viewPlan.GetViewRange();

                            var depthLevel = viewRange.GetLevelId(PlanViewPlane.ViewDepthPlane).ToElement<Level>(_doc);
                            var depthOffset = viewRange.GetOffset(PlanViewPlane.ViewDepthPlane);

                            viewPlan.CropBoxVisible = true;
                            viewPlan.CropBoxVisible = false;

                            var cropBoxBB = viewPlan.CropBox;

                            if (viewPlan.GetUnderlayOrientation() == UnderlayOrientation.LookingDown)
                            {
                                var topLevel = viewRange.GetLevelId(PlanViewPlane.TopClipPlane).ToElement<Level>(_doc);
                                var topOffset = viewRange.GetOffset(PlanViewPlane.TopClipPlane);

                                var minElevation = depthLevel != null
                                    ? depthLevel.ProjectElevation + depthOffset
                                    : -1000;
                                var maxElevation = topLevel != null
                                    ? topLevel.ProjectElevation + topOffset
                                    : 1000;

                                minPoint = new XYZ(cropBoxBB.Min.X, cropBoxBB.Min.Y, minElevation);
                                maxPoint = new XYZ(cropBoxBB.Max.X, cropBoxBB.Max.Y, maxElevation);
                            }
                            else
                            {
                                var cutLevel = viewRange.GetLevelId(PlanViewPlane.CutPlane).ToElement<Level>(_doc);
                                var cutOffset = viewRange.GetOffset(PlanViewPlane.CutPlane);

                                var minElevation = cutLevel != null
                                    ? cutLevel.ProjectElevation + cutOffset
                                    : -1000;
                                var maxElevation = depthLevel != null
                                    ? depthLevel.ProjectElevation + depthOffset
                                    : 1000;

                                minPoint = new XYZ(cropBoxBB.Min.X, cropBoxBB.Min.Y, minElevation);
                                maxPoint = new XYZ(cropBoxBB.Max.X, cropBoxBB.Max.Y, maxElevation);
                            }
                        }
                        else if (View is ViewSection viewSection)
                        {
                            var cropBoxBB = viewSection.CropBox;
                            var transform = cropBoxBB.Transform;

                            minPoint = transform.OfPoint(cropBoxBB.Min);
                            maxPoint = transform.OfPoint(cropBoxBB.Max);
                        }

                        var ogs = GetOverrideGS(Color);

                        DirectShapeId = ExMethods.CreateDirectShape(new List<GeometryObject> { CreateSolid(minPoint, maxPoint) }).Id;
                        _view3D.SetElementOverrides(DirectShapeId, ogs);

                        t.Commit();
                    }

                    if (_showSectionPlane)
                    {
                        SectionPlane();
                    }

                    tGroup.Assimilate();
                }
            }
            private void DeleteDS()
            {
                using (Transaction t = new Transaction(_doc, "Удаление диапазона видимости"))
                {
                    t.Start();

                    _doc.Delete(DirectShapeId);
                    DirectShapeId = null;

                    if (SectionPlaneId != null)
                    {
                        _doc.Delete(SectionPlaneId);
                        SectionPlaneId = null;
                    }

                    t.Commit();
                }
            }

            private void SectBox()
            {
                var bbox = DirectShapeId.ToElement(_doc).get_BoundingBox(_view3D);

                using (Transaction t = new Transaction(_doc, "Обрезка вида по диапазону видимости"))
                {
                    t.Start();

                    _view3D.SetSectionBox(bbox);

                    t.Commit();
                }

                _uiDoc.ActiveView = _view3D;
                _view3D.ToUIView()?.ZoomToFit();
            }

            private void SectionPlane()
            {
                if (View is ViewPlan viewPlan)
                {
                    var viewRange = viewPlan.GetViewRange();

                    var cutLevel = viewRange.GetLevelId(PlanViewPlane.CutPlane).ToElement<Level>(_doc);
                    var cutOffset = viewRange.GetOffset(PlanViewPlane.CutPlane);

                    var cutElevation = cutLevel.ProjectElevation + cutOffset;

                    var cropBoxBB = viewPlan.CropBox;
                    var minCutPoint = new XYZ(cropBoxBB.Min.X, cropBoxBB.Min.Y, cutElevation);
                    var maxCutPoint = new XYZ(cropBoxBB.Max.X, cropBoxBB.Max.Y, cutElevation);

                    var ogs = GetOverrideGS(Color);

                    using (Transaction t = new Transaction(_doc, "Показать секущую плоскость диапазона видимости"))
                    {
                        t.Start();

                        SectionPlaneId = ExMethods.CreateDirectShape(new List<GeometryObject> { CreateCutSolid(minCutPoint, maxCutPoint) }).Id;
                        _view3D.SetElementOverrides(SectionPlaneId, ogs);

                        t.Commit();
                    }
                }
            }


            private Solid CreateSolid(XYZ min, XYZ max)
            {
                var profile = new CurveLoop();
                profile.Append(Line.CreateBound(new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z)));
                profile.Append(Line.CreateBound(new XYZ(max.X, min.Y, min.Z), new XYZ(max.X, max.Y, min.Z)));
                profile.Append(Line.CreateBound(new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z)));
                profile.Append(Line.CreateBound(new XYZ(min.X, max.Y, min.Z), new XYZ(min.X, min.Y, min.Z)));

                return GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { profile },
                    XYZ.BasisZ,
                    max.Z - min.Z);
            }
            private Solid CreateCutSolid(XYZ min, XYZ max)
            {
                var profile = new CurveLoop();
                profile.Append(Line.CreateBound(new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z)));
                profile.Append(Line.CreateBound(new XYZ(max.X, min.Y, min.Z), new XYZ(max.X, max.Y, min.Z)));
                profile.Append(Line.CreateBound(new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z)));
                profile.Append(Line.CreateBound(new XYZ(min.X, max.Y, min.Z), new XYZ(min.X, min.Y, min.Z)));

                return GeometryCreationUtilities.CreateExtrusionGeometry(
                    new List<CurveLoop> { profile },
                    XYZ.BasisZ,
                    0.00001.FromMillimeters()
                );
            }

            private OverrideGraphicSettings GetOverrideGS(Color color)
            {
                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                ogs.SetSurfaceTransparency(70);

                ogs.SetSurfaceForegroundPatternId(_patternId);
                ogs.SetSurfaceForegroundPatternColor(color);
                ogs.SetSurfaceBackgroundPatternId(_patternId);
                ogs.SetSurfaceBackgroundPatternColor(color);
                ogs.SetCutForegroundPatternId(_patternId);
                ogs.SetCutForegroundPatternColor(color);
                ogs.SetCutBackgroundPatternId(_patternId);
                ogs.SetCutBackgroundPatternColor(color);

                ogs.SetProjectionLineColor(color);
                ogs.SetCutLineColor(color);

                return ogs;
            }
        }
    }
}