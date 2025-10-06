using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using BIMPlugins.ExtStorage.Methods;
using System.Linq;
using System.Collections.Generic;
using System;


namespace BIMPlugins.Views.WPF
{
    public partial class CropBoxViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<ViewCropBoxItem> _viewCropBoxItems = [];
        [ObservableProperty] private static bool _showSectionPlane = true;
        [ObservableProperty] private string _filter;
        [ObservableProperty] private ICollectionView _filteredItems;

        private ExternalEvent DeleteSectionPlaneExEvent { get; set; }
        public ExternalEvent DeleteDirectShapesExEvent { get; set; }

        partial void OnFilterChanged(string value)
        {
            FilteredItems.Refresh();
        }
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
            var handler = new RevitAPI.MyEventHandler<CropBoxViewModel>(
                    this,
                    vm => vm.DeletePlanes()
                );
            DeleteSectionPlaneExEvent = ExternalEvent.Create(handler);

            var deleteAllHandler = new RevitAPI.MyEventHandler<CropBoxViewModel>(
                    this,
                    vm => vm.DeleteAllDirectShapes()
                );
            DeleteDirectShapesExEvent = ExternalEvent.Create(deleteAllHandler);

            GetView3D(RevitAPI.Document);

            _patternId = new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(FillPatternElement))
                .FirstOrDefault(p => p.Name == "<Сплошная заливка>")
                .Id;

            var filter = new ElementMulticlassFilter(new List<Type> { typeof(ViewPlan), typeof(ViewSection)});
            var views = new FilteredElementCollector(RevitAPI.Document)
                .WherePasses(filter)
                .Cast<View>()
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
            using (Transaction t = new Transaction(RevitAPI.Document, "Скрыть секущую плоскость диапазона видимости"))
            {
                t.Start();

                foreach (var viewCropBox in ViewCropBoxItems.Where(v => v.SectionPlaneId != null).ToList())
                {
                    RevitAPI.Document.Delete(viewCropBox.SectionPlaneId);
                    viewCropBox.SectionPlaneId = null;
                }

                t.Commit();
            }
        }
        private void DeleteAllDirectShapes()
        {
            using (Transaction t = new Transaction(RevitAPI.Document, "Удаление всех диапазонов видимости"))
            {
                t.Start();

                foreach (var viewCropBox in ViewCropBoxItems.Where(v => v.IsSelected).ToList())
                {
                    RevitAPI.Document.Delete(viewCropBox.DirectShapeId);

                    if (viewCropBox.SectionPlaneId != null) RevitAPI.Document.Delete(viewCropBox.SectionPlaneId);
                }

                t.Commit();
            }
        }


        private void GetView3D(Document doc)
        {
            if (doc.ActiveView is View3D)
            {
                _view3D = doc.ActiveView as View3D;
            }
            else
            {
                if (doc.IsWorkshared)
                {
                    _view3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .ToElements()
                        .Where(e => e.Name == "{3D - " + RevitAPI.Application.Username + "}")
                        .FirstOrDefault() as View3D;
                }
                else
                {
                    _view3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .ToElements()
                        .Where(e => e.Name == "{3D}")
                        .FirstOrDefault() as View3D;
                }
                
                if (_view3D == null) _view3D = Create3DView(doc);

                UIDocument uidoc = new UIDocument(doc);
                uidoc.ActiveView = _view3D;
            }
        }
        private View3D Create3DView(Document doc)
        {
            var viewTypeId = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional)
                .Id;

            View3D view3D;
            using (Transaction t = new Transaction(doc, "Создание 3D вида"))
            {
                t.Start();

                view3D = View3D.CreateIsometric(doc, viewTypeId);
                view3D.Name = "3D";

                t.Commit();
            }

            return view3D;
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

            public ExternalEvent SectionPlaneExEvent {  get; set; }

            public ElementId DirectShapeId { get; set; }
            public ElementId SectionPlaneId { get; set; }

            private ExternalEvent CreateExEvent { get; set; }
            private ExternalEvent DeleteExEvent { get; set; }
            private ExternalEvent SectBoxExEvent { get; set; }

            partial void OnIsSelectedChanging(bool value)
            {
                RevitAPI.UIDocument.ActiveView = _view3D;

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
                var createHandler = new RevitAPI.MyEventHandler<ViewCropBoxItem>(
                    this,
                    vm => vm.CreateDS()
                );
                CreateExEvent = ExternalEvent.Create(createHandler);

                var deleteHandler = new RevitAPI.MyEventHandler<ViewCropBoxItem>(
                    this,
                    vm => vm.DeleteDS()
                );
                DeleteExEvent = ExternalEvent.Create(deleteHandler);

                var sectBoxHandler = new RevitAPI.MyEventHandler<ViewCropBoxItem>(
                    this,
                    vm => vm.SectBox()
                );
                SectBoxExEvent = ExternalEvent.Create(sectBoxHandler);

                var sectPlaneHandler = new RevitAPI.MyEventHandler<ViewCropBoxItem>(
                    this,
                    vm => vm.SectionPlane()
                );
                SectionPlaneExEvent = ExternalEvent.Create(sectPlaneHandler);

                View = view;
                ViewName = view.Title.Replace("_", "__");
                Color = color;
            }


            [RelayCommand]
            private void OpenView()
            {
                RevitAPI.UIDocument.ActiveView = View;

                var uiView = RevitAPI.UIDocument.GetOpenUIViews().FirstOrDefault(v => v.ViewId.ToString() == View.Id.ToString());
                uiView.ZoomToFit();
            }

            [RelayCommand]
            private void SectionBox() => SectBoxExEvent.Raise();

            [RelayCommand]
            private void Select()
            {
                RevitAPI.UIDocument.Selection.SetElementIds([DirectShapeId]);
            }

            private void CreateDS()
            {
                var minPoint = new XYZ();
                var maxPoint = new XYZ();

                using (TransactionGroup tGroup = new TransactionGroup(RevitAPI.Document, "Создание диапазона видимости"))
                {
                    tGroup.Start();

                    using (Transaction t = new Transaction(RevitAPI.Document, "Создание диапазона видимости"))
                    {
                        t.Start();

                        if (View is ViewPlan viewPlan)
                        {
                            var viewRange = viewPlan.GetViewRange();

                            var depthLevel = viewRange.GetLevelId(PlanViewPlane.ViewDepthPlane).ToElement<Level>();
                            var depthOffset = viewRange.GetOffset(PlanViewPlane.ViewDepthPlane);

                            viewPlan.CropBoxVisible = true;
                            viewPlan.CropBoxVisible = false;

                            var cropBoxBB = viewPlan.CropBox;

                            if (viewPlan.GetUnderlayOrientation() == UnderlayOrientation.LookingDown)
                            {
                                var topLevel = viewRange.GetLevelId(PlanViewPlane.TopClipPlane).ToElement<Level>();
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
                                var cutLevel = viewRange.GetLevelId(PlanViewPlane.CutPlane).ToElement<Level>();
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
                using (Transaction t = new Transaction(RevitAPI.Document, "Удаление диапазона видимости"))
                {
                    t.Start();

                    RevitAPI.Document.Delete(DirectShapeId);
                    DirectShapeId = null;

                    if (SectionPlaneId != null)
                    {
                        RevitAPI.Document.Delete(SectionPlaneId);
                        SectionPlaneId = null;
                    }

                    t.Commit();
                }
            }

            private void SectBox()
            {
                var bbox = DirectShapeId.ToElement().get_BoundingBox(_view3D);

                using (Transaction t = new Transaction(RevitAPI.Document, "Обрезка вида по диапазону видимости"))
                {
                    t.Start();

                    _view3D.SetSectionBox(bbox);

                    t.Commit();
                }

                RevitAPI.UIDocument.ActiveView = _view3D;

                var uiView = RevitAPI.UIDocument.GetOpenUIViews().FirstOrDefault(v => v.ViewId.ToString() == _view3D.Id.ToString());
                uiView.ZoomToFit();
            }

            private void SectionPlane()
            {
                if (View is ViewPlan viewPlan)
                {
                    var viewRange = viewPlan.GetViewRange();

                    var cutLevel = viewRange.GetLevelId(PlanViewPlane.CutPlane).ToElement<Level>();
                    var cutOffset = viewRange.GetOffset(PlanViewPlane.CutPlane);

                    var cutElevation = cutLevel.ProjectElevation + cutOffset;

                    var cropBoxBB = viewPlan.CropBox;
                    var minCutPoint = new XYZ(cropBoxBB.Min.X, cropBoxBB.Min.Y, cutElevation);
                    var maxCutPoint = new XYZ(cropBoxBB.Max.X, cropBoxBB.Max.Y, cutElevation);

                    var ogs = GetOverrideGS(Color);

                    using (Transaction t = new Transaction(RevitAPI.Document, "Показать секущую плоскость диапазона видимости"))
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
                    UnitUtils.ConvertToInternalUnits(0.00001, ParameterMethods.GetUnitType()));
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