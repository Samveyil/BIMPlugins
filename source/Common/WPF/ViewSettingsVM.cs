using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class ViewSettingsVM : ObservableObject
    {
        private const string Show = "Показать";
        private const string Hide = "Скрыть";
        private const string GlobalVisible = "Глобальная настройка (видимые)";
        private const string GlobalHidden = "Глобальная настройка (скрытые)";

        [ObservableProperty] private ObservableCollection<ViewFilter> _viewFilters = [];
        [ObservableProperty] private ObservableCollection<ViewWorkset> _viewWorksets = [];

        // Every click is queued and applied by one external event, so quick clicks are not lost while Revit is busy.
        private readonly Queue<VisibilityChange> _changes = new();
        private readonly ExternalEvent _applyEvent;

        public ViewSettingsVM()
        {
            _applyEvent = RevitAPI.CreateExtEvent(this, vm => vm.ApplyChanges());
        }

        /// <summary>Reloads the filters and worksets of the active view.</summary>
        public void Refresh()
        {
            ViewFilters.Clear();
            ViewWorksets.Clear();

            var view = RevitAPI.UIApplication.ActiveUIDocument?.ActiveView;
            if (view == null) return;

            LoadFilters(view);
            LoadWorksets(view);
        }

        private void LoadFilters(View view)
        {
            try
            {
                var doc = view.Document;

                foreach (var filter in view.GetFilters()
                    .Select(id => id.ToElement(doc))
                    .OrderBy(f => f.Name, NaturalComparer.CurrentCultureIgnoreCase))
                {
                    ViewFilters.Add(new ViewFilter
                    {
                        Name = filter.Name,
                        FilterId = filter.Id,
                        IsSelected = view.GetFilterVisibility(filter.Id),
                        Owner = this
                    });
                }
            }
            catch { } // Views without visibility settings, such as sheets and schedules, have no filters.
        }

        private void LoadWorksets(View view)
        {
            var doc = view.Document;
            if (!doc.IsWorkshared) return;

            try
            {
                foreach (var workset in new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .ToWorksets()
                    .OrderBy(w => w.Name, NaturalComparer.CurrentCultureIgnoreCase))
                {
                    ViewWorksets.Add(new ViewWorkset
                    {
                        Name = workset.Name,
                        WorksetId = workset.Id,
                        VisibilityModes = [Show, Hide, workset.IsVisibleByDefault ? GlobalVisible : GlobalHidden],
                        Visibility = ToText(view.GetWorksetVisibility(workset.Id), workset.IsVisibleByDefault),
                        Owner = this
                    });
                }
            }
            catch { } // Views without visibility settings have no workset visibility.
        }

        private static string ToText(WorksetVisibility visibility, bool isVisibleByDefault) => visibility switch
        {
            WorksetVisibility.Visible => Show,
            WorksetVisibility.Hidden => Hide,
            _ => isVisibleByDefault ? GlobalVisible : GlobalHidden
        };

        private static WorksetVisibility ToWorksetVisibility(string visibility) => visibility switch
        {
            Show => WorksetVisibility.Visible,
            Hide => WorksetVisibility.Hidden,
            _ => WorksetVisibility.UseGlobalSetting
        };

        private void Enqueue(VisibilityChange change)
        {
            _changes.Enqueue(change);
            _applyEvent.Raise();
        }

        private void ApplyChanges()
        {
            var view = RevitAPI.UIApplication.ActiveUIDocument?.ActiveView;
            if (view == null)
            {
                _changes.Clear();
                return;
            }

            var changes = new List<VisibilityChange>();
            while (_changes.Count > 0)
            {
                var change = _changes.Dequeue();
                if (change.IsChanged(view))
                    changes.Add(change);
            }

            if (changes.Count == 0) return;

            using (Transaction t = new Transaction(view.Document, "Изменить видимость"))
            {
                t.Start();

                if (view.ViewTemplateId != ElementId.InvalidElementId)
                    view.EnableTemporaryViewPropertiesMode(view.Id);

                foreach (var change in changes)
                    change.Apply(view);

                t.Commit();
            }
        }

        private sealed class VisibilityChange(Func<View, bool> isChanged, Action<View> apply)
        {
            public Func<View, bool> IsChanged { get; } = isChanged;
            public Action<View> Apply { get; } = apply;
        }

        public partial class ViewFilter : ObservableObject
        {
            [ObservableProperty] private bool _isSelected;

            public string Name { get; set; }
            public ElementId FilterId { get; set; }

            // Set last when the list loads, so that loading itself changes nothing.
            internal ViewSettingsVM Owner { get; set; }

            partial void OnIsSelectedChanged(bool value)
            {
                var filterId = FilterId;
                Owner?.Enqueue(new VisibilityChange(
                    view => view.GetFilterVisibility(filterId) != value,
                    view => view.SetFilterVisibility(filterId, value)));
            }
        }

        public partial class ViewWorkset : ObservableObject
        {
            [ObservableProperty] private string _visibility;

            public string Name { get; set; }
            public WorksetId WorksetId { get; set; }
            public List<string> VisibilityModes { get; set; } = [];

            internal ViewSettingsVM Owner { get; set; }

            partial void OnVisibilityChanged(string value)
            {
                var worksetId = WorksetId;
                var visibility = ToWorksetVisibility(value);
                Owner?.Enqueue(new VisibilityChange(
                    view => view.GetWorksetVisibility(worksetId) != visibility,
                    view => view.SetWorksetVisibility(worksetId, visibility)));
            }
        }
    }
}
