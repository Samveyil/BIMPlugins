using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Classes;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
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
    /// View ranges of plans and sections shown in a 3D view as temporary graphics: nothing is added to the model,
    /// so there is nothing to delete and nothing in the undo list.
    /// </summary>
    public partial class CropBoxVM : ObservableObject
    {
        // The choice of the last window is kept for the next one.
        private static bool _lastShowSectionPlane = true;

        [ObservableProperty] private bool _showSectionPlane = _lastShowSectionPlane;
        [ObservableProperty] private string _filter;

        // Everything that needs Revit runs in one external event, in the order it was asked for.
        private readonly Queue<Action> _actions = new();
        private readonly ExternalEvent _actionsEvent;

        public CropBoxVM()
        {
            UIDoc = RevitAPI.UIDocument;
            Doc = UIDoc.Document;
            View3D = ViewGraphics.GetUser3DView(Doc);

            _actionsEvent = RevitAPI.CreateExtEvent(this, vm => vm.RunActions());

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

        public List<ViewCropBoxItem> ViewCropBoxItems { get; }
        public ICollectionView FilteredItems { get; }

        partial void OnFilterChanged(string value) => FilteredItems.Refresh();

        partial void OnShowSectionPlaneChanged(bool value)
        {
            _lastShowSectionPlane = value;
            Run(UpdateShapes);
        }

        internal void Run(Action action)
        {
            _actions.Enqueue(action);
            _actionsEvent.Raise();
        }

        private void RunActions()
        {
            while (_actions.Count > 0)
                _actions.Dequeue()();
        }

        /// <summary>Shows the ranges of the ticked views and hides the others.</summary>
        internal void UpdateShapes()
        {
            foreach (var item in ViewCropBoxItems)
            {
                if (item.IsSelected)
                    TemporaryGraphics.Set(item, Doc, item.CreateShape(ShowSectionPlane));
                else
                    TemporaryGraphics.Remove(item);
            }

            if (ViewCropBoxItems.Any(v => v.IsSelected) && UIDoc.ActiveView.Id != View3D.Id)
                UIDoc.ActiveView = View3D;

            UIDoc.UpdateAllOpenViews();
        }

        /// <summary>Hides all the ranges when the window closes.</summary>
        public void HideAll() => Run(() =>
        {
            TemporaryGraphics.RemoveAll(ViewCropBoxItems);
            UIDoc.UpdateAllOpenViews();
        });
    }

    public partial class ViewCropBoxItem(CropBoxVM owner, View view, Color color) : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        private readonly CropBoxVM _owner = owner;

        // The range of the view, read from Revit when it is first shown.
        private bool _isRangeRead;
        private XYZ _min;
        private XYZ _max;
        private double? _cutElevation;

        public View View { get; } = view;
        public string ViewName { get; } = view.Title;
        public Color Color { get; } = color;

        partial void OnIsSelectedChanged(bool value) => _owner.Run(_owner.UpdateShapes);

        [RelayCommand]
        private void OpenView()
        {
            _owner.UIDoc.ActiveView = View;
            View.ToUIView()?.ZoomToFit();
        }

        [RelayCommand]
        private void SectionBox() => _owner.Run(SetSectionBox);

        [RelayCommand]
        private void ShowRange() => _owner.Run(ZoomToRange);

        /// <summary>The range as a see-through box, with the cut plane of a plan if asked.</summary>
        internal TemporaryShape CreateShape(bool withCutPlane)
        {
            ReadRange();

            var shape = new TemporaryShape(Color).AddBox(_min, _max);
            if (withCutPlane && _cutElevation is double cutElevation)
                shape.AddRectangle(_min, _max, cutElevation);

            return shape;
        }

        private void SetSectionBox()
        {
            ReadRange();

            var view3D = _owner.View3D;
            using (Transaction t = new Transaction(_owner.Doc, "Обрезка вида по диапазону видимости"))
            {
                t.Start();
                view3D.SetSectionBox(new BoundingBoxXYZ { Min = _min, Max = _max });
                t.Commit();
            }

            _owner.UIDoc.ActiveView = view3D;
            view3D.ToUIView()?.ZoomToFit();
        }

        private void ZoomToRange()
        {
            ReadRange();

            var view3D = _owner.View3D;
            _owner.UIDoc.ActiveView = view3D;
            view3D.ToUIView()?.ZoomAndCenterRectangle(_min, _max);
        }

        // The corners of the view range of a plan, or of the crop box of a section, in model coordinates.
        private void ReadRange()
        {
            if (_isRangeRead) return;
            _isRangeRead = true;

            XYZ a, b;
            if (View is ViewPlan viewPlan)
            {
                var viewRange = viewPlan.GetViewRange();
                var depthElevation = GetElevation(viewRange, PlanViewPlane.ViewDepthPlane);
                _cutElevation = GetElevation(viewRange, PlanViewPlane.CutPlane);

                var cropBox = ReadPlanCropBox(viewPlan);

                var (minElevation, maxElevation) = viewPlan.GetUnderlayOrientation() == UnderlayOrientation.LookingDown
                    ? (depthElevation ?? -1000, GetElevation(viewRange, PlanViewPlane.TopClipPlane) ?? 1000)
                    : (_cutElevation ?? -1000, depthElevation ?? 1000);

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
            _min = new XYZ(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
            _max = new XYZ(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        }

        // Showing the crop box for a moment makes Revit compute it. The change is rolled back, so the view stays
        // as the user had it and nothing goes into the undo list.
        private BoundingBoxXYZ ReadPlanCropBox(ViewPlan viewPlan)
        {
            using (TransactionGroup tGroup = new TransactionGroup(_owner.Doc, "Чтение рамки обрезки"))
            {
                tGroup.Start();

                using (Transaction t = new Transaction(_owner.Doc, "Чтение рамки обрезки"))
                {
                    t.Start();
                    viewPlan.CropBoxVisible = true;
                    t.Commit();
                }

                var cropBox = viewPlan.CropBox;

                tGroup.RollBack();
                return cropBox;
            }
        }

        // The elevation of a plane of the view range; null if the plane is not bound to a level.
        private double? GetElevation(PlanViewRange viewRange, PlanViewPlane plane)
        {
            var level = viewRange.GetLevelId(plane).ToElement<Level>(_owner.Doc);
            return level == null ? null : level.ProjectElevation + viewRange.GetOffset(plane);
        }
    }
}
