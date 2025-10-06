using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class ViewSettingsViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<ViewFilter> _viewFilters = [];
        [ObservableProperty] private ObservableCollection<ViewWorkset> _viewWorksets = [];

        private static ExternalEvent WorksetEvent { get; set; }
        private static ExternalEvent FilterEvent { get; set; }

        private static WorksetId SelectedWorksetId { get; set; }
        private static string SelectedWorksetVisibility { get; set; }
        private static ElementId SelectedFilterId { get; set; }
        private static bool SelectedFilterMode { get; set; }


        public ViewSettingsViewModel()
        {
            var filterHandler = new RevitAPI.MyEventHandler<ViewSettingsViewModel>(
                this,
                vm => vm.ChangeFilterVisibility()
            );
            FilterEvent = ExternalEvent.Create(filterHandler);

            var worksetHandler = new RevitAPI.MyEventHandler<ViewSettingsViewModel>(
                this,
                vm => vm.ChangeWorksetVisibility()
            );
            WorksetEvent = ExternalEvent.Create(worksetHandler);
        }

        public void GetViewFilters()
        {
            ViewFilters.Clear();
            try
            {
                List<ViewFilter> viewFilters = [];
                var activeView = RevitAPI.UIDocument.ActiveView;
                foreach (ElementId filterId in activeView.GetFilters())
                {
                    Element filter = filterId.ToElement();
                    ViewFilter viewFilter = new ViewFilter
                    {
                        Name = filter.Name,
                        FilterId = filterId,
                        IsSelected = activeView.GetFilterVisibility(filterId)
                    };
                    viewFilters.Add(viewFilter);
                }
                viewFilters = viewFilters.OrderBy(x => x.Name).ToList();
                foreach (ViewFilter viewFilter in viewFilters)
                {
                    ViewFilters.Add(viewFilter);
                }
            }
            catch { }
        }
        public void GetViewWorksets()
        {
            ViewWorksets.Clear();
            try
            {
                var worksets = new FilteredWorksetCollector(RevitAPI.Document)
                    .OfKind(WorksetKind.UserWorkset)
                    .ToWorksets().ToList();
                
                List<ViewWorkset> viewWorksets = [];
                foreach (Workset workset in worksets)
                {
                    ViewWorkset viewWorkset = new ViewWorkset
                    {
                        Name = workset.Name,
                        WorksetId = workset.Id,
                        Workset = workset,
                        Visibility = GetWorksetVisibility(workset),
                        VisibilityModes = GetWorksetVisibilityModes(workset)
                    };
                    viewWorksets.Add(viewWorkset);
                }
                
                viewWorksets = viewWorksets.OrderBy(x => x.Name).ToList();
                foreach (ViewWorkset viewWorkset in viewWorksets)
                {
                    ViewWorksets.Add(viewWorkset);
                }
            }
            catch { }
        }

        private string GetWorksetVisibility(Workset workset)
        {
            WorksetVisibility worksetVisibility = RevitAPI.UIDocument.ActiveView.GetWorksetVisibility(workset.Id);
            if (worksetVisibility == WorksetVisibility.Visible)
            {
                return "Показать";
            }
            else if (worksetVisibility == WorksetVisibility.Hidden)
            {
                return "Скрыть";
            }
            else if (workset.IsVisibleByDefault)
            {
                return "Глобальная настройка (видимые)";
            }
            else
            {
                return "Глобальная настройка (скрытые)";
            }
        }
        private List<string> GetWorksetVisibilityModes(Workset workset)
        {
            List<string> visibilityModes = new List<string>()
            {
                "Показать",
                "Скрыть"
            };
            if (workset.IsVisibleByDefault)
            {
                visibilityModes.Add("Глобальная настройка (видимые)");
            }
            else
            {
                visibilityModes.Add("Глобальная настройка (скрытые)");
            }
            return visibilityModes;
        }


        private void ChangeFilterVisibility()
        {
            var view = RevitAPI.UIDocument.ActiveView;
            var prefVisibility = view.GetFilterVisibility(SelectedFilterId);
            if (prefVisibility != SelectedFilterMode)
            {
                using (Transaction t = new Transaction(RevitAPI.Document, "Изменить видимость фильтра"))
                {
                    t.Start();

                    var templateId = RevitAPI.UIDocument.ActiveView.ViewTemplateId;
                    if (templateId.GetValue() == -1)
                    {
                        view.SetFilterVisibility(SelectedFilterId, SelectedFilterMode);
                    }
                    else
                    {
                        view.EnableTemporaryViewPropertiesMode(view.Id);
                        view.SetFilterVisibility(SelectedFilterId, SelectedFilterMode);
                    }

                    t.Commit();
                }
            }
            
        }
        private void ChangeWorksetVisibility()
        {
            View view = RevitAPI.UIDocument.ActiveView;

            WorksetVisibility worksetVisibility = ConvertStringToWorksetVisibility(SelectedWorksetVisibility);
            var prefVisibility = view.GetWorksetVisibility(SelectedWorksetId);
            if (prefVisibility != worksetVisibility)
            {
                using (Transaction t = new Transaction(RevitAPI.Document, "Изменить видимость рабочего набора"))
                {
                    t.Start();

                    var templateId = RevitAPI.UIDocument.ActiveView.ViewTemplateId;
                    if (templateId.GetValue() == -1)
                    {
                        view.SetWorksetVisibility(SelectedWorksetId, worksetVisibility);
                    }
                    else
                    {
                        view.EnableTemporaryViewPropertiesMode(view.Id);
                        view.SetWorksetVisibility(SelectedWorksetId, worksetVisibility);
                    }

                    t.Commit();
                }
            } 
        }
        private WorksetVisibility ConvertStringToWorksetVisibility(string visibility)
        {
            if (visibility == "Показать")
            {
                return WorksetVisibility.Visible;
            }
            else if (visibility == "Скрыть")
            {
                return WorksetVisibility.Hidden;
            }
            else
            {
                return WorksetVisibility.UseGlobalSetting;
            }
        }


        public partial class ViewFilter : ObservableObject
        {
            [ObservableProperty] private bool _isSelected;

            public string Name { get; set; }
            public ElementId FilterId { get; set; }

            partial void OnIsSelectedChanged(bool value)
            {
                SelectedFilterId = FilterId;
                SelectedFilterMode = value;

                FilterEvent.Raise();
            }
        }
        public partial class ViewWorkset : ObservableObject
        {
            [ObservableProperty] private string _visibility;

            public string Name { get; set; }
            public Workset Workset { get; set; }
            public WorksetId WorksetId { get; set; }
            public List<string> VisibilityModes { get; set; } = [];

            partial void OnVisibilityChanged(string value)
            {
                SelectedWorksetId = WorksetId;
                SelectedWorksetVisibility = value;

                WorksetEvent.Raise();
            }
        }
    }
}