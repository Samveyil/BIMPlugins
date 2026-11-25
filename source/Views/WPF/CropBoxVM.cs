using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.ExtStorage.Methods;
using BIMPlugins.Views.Classes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// View ranges of plans and sections shown in a 3D view as DirectShapes. The shapes live only while the window is open:
    /// they are deleted when it closes, and shapes left by a window that did not close (Revit failed) are deleted when the next one opens.
    /// </summary>
    public partial class CropBoxVM : ObservableObject
    {
        // Every shape is marked so, to tell it from any other DirectShape of the model.
        private const string ShapeApplicationId = "BIMPlugins";
        private const string ShapeDataId = "CropBox";

        // The choice of the last window is kept for the next one.
        private static bool _lastShowSectionPlane = true;

        [ObservableProperty] private bool _showSectionPlane = _lastShowSectionPlane;
        [ObservableProperty] private string _filter;

        private readonly ExternalEvent _deletePlanesEvent;

        public CropBoxVM()
        {
            UIDoc = RevitAPI.UIDocument;
            Doc = UIDoc.Document;

            DeleteLeftShapes();

            _deletePlanesEvent = RevitAPI.CreateExtEvent(this, vm => vm.DeletePlanes());
            DeleteShapesEvent = RevitAPI.CreateExtEvent(this, vm => vm.DeleteAllShapes());

            View3D = ViewGraphics.GetUser3DView(Doc);
            PatternId = ViewGraphics.GetSolidFillPatternId(Doc);

            var viewClasses = new ElementMulticlassFilter(new List<Type> { typeof(ViewPlan), typeof(ViewSection) });
            ViewCropBoxItems = Doc.ToElements<View>(viewClasses)
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.Title, NaturalComparer.CurrentCultureIgnoreCase)
                .Select(v => new ViewCropBoxItem(this, v, ViewGraphics.NextDistinctColor()))
                .ToList();

            FilteredItems = CollectionViewSource.GetDefaultView(ViewCropBoxItems);
            FilteredItems.Filter = item =>
                Filter.IsNullOrEmpty() || ((ViewCropBoxItem)item).ViewName.Contains(Filter, StringComparison.OrdinalIgnoreCase);
        }

        internal UIDocument UIDoc { get; }
        internal Document Doc { get; }
        internal View3D View3D { get; }
        internal ElementId PatternId { get; }

        /// <summary>Deletes the shapes when the window closes.</summary>
        public ExternalEvent DeleteShapesEvent { get; }

        public List<ViewCropBoxItem> ViewCropBoxItems { get; }
        public ICollectionView FilteredItems { get; }

        partial void OnFilterChanged(string value) => FilteredItems.Refresh();

        partial void OnShowSectionPlaneChanged(bool value)
        {
            _lastShowSectionPlane = value;

            if (value)
            {
                foreach (var item in ViewCropBoxItems.Where(v => v.IsSelected))
                    item.SectionPlaneEvent.Raise();
            }
            else
            {
                // One event deletes the planes of all the views.
                _deletePlanesEvent.Raise();
            }
        }

        /// <summary>A shape in the 3D view, marked as one of this window's.</summary>
        internal ElementId CreateShape(Solid solid, Color color)
        {
            var shape = ExMethods.CreateDirectShape(Doc, [solid]);
            shape.ApplicationId = ShapeApplicationId;
            shape.ApplicationDataId = ShapeDataId;

            View3D.SetElementOverrides(shape.Id, GetOverrides(color));
            return shape.Id;
        }

        // Shapes of a window that never closed, when Revit failed: they must not stay in the model.
        private void DeleteLeftShapes()
        {
            var leftShapeIds = Doc.ToElements<DirectShape>()
                .Where(s => s.ApplicationId == ShapeApplicationId && s.ApplicationDataId == ShapeDataId)
                .Select(s => s.Id)
                .ToList();

            if (leftShapeIds.Count == 0) return;

            using (Transaction t = new Transaction(Doc, "Удаление оставшихся диапазонов видимости"))
            {
                t.Start();
                Doc.Delete(leftShapeIds);
                t.Commit();
            }
        }

        private void DeletePlanes()
        {
            using (Transaction t = new Transaction(Doc, "Скрыть секущую плоскость диапазона видимости"))
            {
                t.Start();

                foreach (var item in ViewCropBoxItems.Where(v => v.SectionPlaneId != null))
                {
                    DeleteIfExists(item.SectionPlaneId);
                    item.SectionPlaneId = null;
                }

                t.Commit();
            }
        }

        private void DeleteAllShapes()
        {
            using (Transaction t = new Transaction(Doc, "Удаление всех диапазонов видимости"))
            {
                t.Start();

                foreach (var item in ViewCropBoxItems.Where(v => v.IsSelected))
                {
                    DeleteIfExists(item.DirectShapeId);
                    DeleteIfExists(item.SectionPlaneId);
                }

                t.Commit();
            }
        }

        // The user may have deleted a shape by hand; deleting it again would fail the whole transaction.
        internal void DeleteIfExists(ElementId id)
        {
            if (id != null && id.ToElement(Doc) != null)
                Doc.Delete(id);
        }

        private OverrideGraphicSettings GetOverrides(Color color)
        {
            var overrides = new OverrideGraphicSettings();
            overrides.SetSurfaceTransparency(70);

            // Without a solid fill in the project the shapes are coloured by their lines alone.
            if (PatternId != null)
            {
                overrides.SetSurfaceForegroundPatternId(PatternId);
                overrides.SetSurfaceForegroundPatternColor(color);
                overrides.SetSurfaceBackgroundPatternId(PatternId);
                overrides.SetSurfaceBackgroundPatternColor(color);
                overrides.SetCutForegroundPatternId(PatternId);
                overrides.SetCutForegroundPatternColor(color);
                overrides.SetCutBackgroundPatternId(PatternId);
                overrides.SetCutBackgroundPatternColor(color);
            }

            overrides.SetProjectionLineColor(color);
            overrides.SetCutLineColor(color);

            return overrides;
        }
    }

    public partial class ViewCropBoxItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        private readonly CropBoxVM _owner;
        private readonly ExternalEvent _createEvent;
        private readonly ExternalEvent _deleteEvent;
        private readonly ExternalEvent _sectionBoxEvent;

        public ViewCropBoxItem(CropBoxVM owner, View view, Color color)
        {
            _owner = owner;
            View = view;
            ViewName = view.Title;
            Color = color;

            _createEvent = RevitAPI.CreateExtEvent(this, item => item.CreateShape());
            _deleteEvent = RevitAPI.CreateExtEvent(this, item => item.DeleteShape());
            _sectionBoxEvent = RevitAPI.CreateExtEvent(this, item => item.SetSectionBox());
            SectionPlaneEvent = RevitAPI.CreateExtEvent(this, item => item.CreateSectionPlane());
        }

        public View View { get; }
        public string ViewName { get; }
        public Color Color { get; }

        public ElementId DirectShapeId { get; set; }
        public ElementId SectionPlaneId { get; set; }
        public ExternalEvent SectionPlaneEvent { get; }

        partial void OnIsSelectedChanged(bool value)
        {
            _owner.UIDoc.ActiveView = _owner.View3D;

            if (value)
                _createEvent.Raise();
            else
                _deleteEvent.Raise();
        }

        [RelayCommand]
        private void OpenView()
        {
            _owner.UIDoc.ActiveView = View;
            View.ToUIView()?.ZoomToFit();
        }

        [RelayCommand]
        private void SectionBox() => _sectionBoxEvent.Raise();

        [RelayCommand]
        private void Select() => _owner.UIDoc.Selection.SetElementIds([DirectShapeId]);

        private void CreateShape()
        {
            var doc = _owner.Doc;

            using (TransactionGroup tGroup = new TransactionGroup(doc, "Создание диапазона видимости"))
            {
                tGroup.Start();

                using (Transaction t = new Transaction(doc, "Создание диапазона видимости"))
                {
                    t.Start();

                    GetRange(out var min, out var max);
                    DirectShapeId = _owner.CreateShape(CreateBox(min, max, max.Z - min.Z), Color);

                    t.Commit();
                }

                if (_owner.ShowSectionPlane)
                    CreateSectionPlane();

                tGroup.Assimilate();
            }
        }

        // The corners of the view range of a plan, or of the crop box of a section, in model coordinates.
        private void GetRange(out XYZ min, out XYZ max)
        {
            XYZ a, b;
            if (View is ViewPlan viewPlan)
            {
                var viewRange = viewPlan.GetViewRange();
                var depthElevation = GetElevation(viewRange, PlanViewPlane.ViewDepthPlane);

                // Showing the crop box for a moment makes Revit compute it; its visibility is then set back as the user had it.
                var cropBoxVisible = viewPlan.CropBoxVisible;
                viewPlan.CropBoxVisible = true;
                viewPlan.CropBoxVisible = cropBoxVisible;

                var cropBox = viewPlan.CropBox;

                var (minElevation, maxElevation) = viewPlan.GetUnderlayOrientation() == UnderlayOrientation.LookingDown
                    ? (depthElevation ?? -1000, GetElevation(viewRange, PlanViewPlane.TopClipPlane) ?? 1000)
                    : (GetElevation(viewRange, PlanViewPlane.CutPlane) ?? -1000, depthElevation ?? 1000);

                a = cropBox.Min.SetZ(minElevation);
                b = cropBox.Max.SetZ(maxElevation);
            }
            else
            {
                var cropBox = View.CropBox;
                a = cropBox.Transform.OfPoint(cropBox.Min);
                b = cropBox.Transform.OfPoint(cropBox.Max);
            }

            // A section looking the other way gives its corners swapped on some axes.
            min = new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
            max = new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        }

        // The elevation of a plane of the view range; null if the plane is not bound to a level.
        private double? GetElevation(PlanViewRange viewRange, PlanViewPlane plane)
        {
            var level = viewRange.GetLevelId(plane).ToElement<Level>(_owner.Doc);
            return level == null ? null : level.ProjectElevation + viewRange.GetOffset(plane);
        }

        private void DeleteShape()
        {
            using (Transaction t = new Transaction(_owner.Doc, "Удаление диапазона видимости"))
            {
                t.Start();

                _owner.DeleteIfExists(DirectShapeId);
                _owner.DeleteIfExists(SectionPlaneId);

                DirectShapeId = null;
                SectionPlaneId = null;

                t.Commit();
            }
        }

        private void SetSectionBox()
        {
            var view3D = _owner.View3D;
            var bbox = DirectShapeId.ToElement(_owner.Doc).get_BoundingBox(view3D);

            using (Transaction t = new Transaction(_owner.Doc, "Обрезка вида по диапазону видимости"))
            {
                t.Start();
                view3D.SetSectionBox(bbox);
                t.Commit();
            }

            _owner.UIDoc.ActiveView = view3D;
            view3D.ToUIView()?.ZoomToFit();
        }

        private void CreateSectionPlane()
        {
            // Only a plan has a cut plane, and only a cut plane bound to a level can be shown.
            if (View is not ViewPlan viewPlan || GetElevation(viewPlan.GetViewRange(), PlanViewPlane.CutPlane) is not double cutElevation)
                return;

            var cropBox = viewPlan.CropBox;

            using (Transaction t = new Transaction(_owner.Doc, "Показать секущую плоскость диапазона видимости"))
            {
                t.Start();

                var plane = CreateBox(cropBox.Min.SetZ(cutElevation), cropBox.Max.SetZ(cutElevation), 0.00001.FromMillimeters());
                SectionPlaneId = _owner.CreateShape(plane, Color);

                t.Commit();
            }
        }

        // A box over the rectangle from min to max at the height of min, extruded up by the given height.
        private static Solid CreateBox(XYZ min, XYZ max, double height)
        {
            var profile = new CurveLoop();
            profile.Append(Line.CreateBound(new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z)));
            profile.Append(Line.CreateBound(new XYZ(max.X, min.Y, min.Z), new XYZ(max.X, max.Y, min.Z)));
            profile.Append(Line.CreateBound(new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z)));
            profile.Append(Line.CreateBound(new XYZ(min.X, max.Y, min.Z), new XYZ(min.X, min.Y, min.Z)));

            return GeometryCreationUtilities.CreateExtrusionGeometry([profile], XYZ.BasisZ, height);
        }
    }
}
