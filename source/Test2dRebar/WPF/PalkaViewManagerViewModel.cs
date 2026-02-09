using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Test2dRebar.WPF
{
    public partial class PalkaViewManagerViewModel : ObservableObject
    {
        [ObservableProperty] private List<ViewTypeItem> _viewTypes = [];

        private readonly Element _palka;
        private readonly List<View> _views;

        public PalkaViewManagerViewModel(List<View> views, Element palka)
        {
            _views = views;
            _palka = palka;

            var viewItems = new List<ViewItem>();
            foreach (var view in _views)
            {
                var item = new ViewItem(view) { IsSelected = view.get_Parameter(new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d")).AsString()?.Contains(_palka.Id.ToString()) ?? false };
                viewItems.Add(item);
            }

            foreach (var g in viewItems.GroupBy(v => v.ViewType).OrderBy(g => g.Key))
            {
                var viewType = new ViewTypeItem(g.Key);
                foreach (var item in g)
                    viewType.Views.Add(item);

                ViewTypes.Add(viewType);
            }
        }

        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();

            var selectedViews = ViewTypes
                .SelectMany(viewType => viewType.Views)
                .Where(viewItem => viewItem.IsSelected)
                .Select(viewItem => viewItem.View)
                .ToList();

            var idPalka = _palka.Id.ToString();

            using (Transaction t = new Transaction(RevitAPI.Document, "Присвоить палку к виду"))
            {
                t.Start();

                foreach (var view in _views)
                {
                    var param = view.get_Parameter(new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d"));
                    if (param.AsString().IsNullOrEmpty())
                        continue;

                    if (param.AsString().Contains(idPalka))
                        param.Set(param.AsString().Replace(idPalka, "").Trim(';'));
                }

                foreach (var view in selectedViews)
                {
                    var param = view.get_Parameter(new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d"));
                    if (param.AsString().IsNullOrEmpty())
                        param.Set(idPalka);
                    else if (!param.AsString().Contains(idPalka))
                        param.Set(string.Join(";", [param.AsString(), idPalka]));
                }

                t.Commit();
            }
        }


        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }

    public partial class ViewItem(View view) : ObservableObject
    {
        [ObservableProperty] private bool _isSelected = false;

        public View View { get; } = view;
        public string Name { get; } = view.Name;
        public string ViewType { get; } = view.Title.Split(':')[0];
        public string ViewId { get; } = view.Id.ToString();
    }
    public class ViewTypeItem(string name)
    {
        public string Name { get; } = name;
        public List<ViewItem> Views { get; } = [];
    }
}