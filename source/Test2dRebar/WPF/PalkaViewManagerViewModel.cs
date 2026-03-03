using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Test2dRebar.Classes;
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

        private readonly Guid _idGuid = RebarMethods.IdGuid;
        private readonly Element _palka;
        private readonly List<View> _views;

        public PalkaViewManagerViewModel(List<View> views, Element palka)
        {
            _views = views;
            _palka = palka;

            var viewItems = new List<ViewItem>();
            foreach (var view in _views.OrderBy(v => v.Title))
            {
                var item = new ViewItem(view) { IsSelected = view.get_Parameter(_idGuid).AsString()?.Contains(_palka.Id.ToString()) ?? false };
                viewItems.Add(item);
            }

            foreach (var g in viewItems.GroupBy(v => v.ViewType))
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

            using (TransactionGroup tGroup = new TransactionGroup(RevitAPI.Document, "Присвоить палку к виду"))
            {
                tGroup.Start();

                var ids = string.Empty;

                using (Transaction t = new Transaction(RevitAPI.Document, "Присвоить палку к виду"))
                {
                    t.Start();

                    foreach (var view in _views)
                    {
                        var param = view.get_Parameter(_idGuid);
                        if (param.IsReadOnly)
                        {
                            TaskDialog.Show("Ошибка", "Параметр OLP_Id заблокирован. Проверьте, что шаблон вида не заблокировал параметр");
                            return;
                        }

                        if (param.AsString().IsNullOrEmpty())
                            continue;

                        if (param.AsString().Contains(idPalka))
                            param.Set(param.AsString().Replace(idPalka, "").Trim(';'));
                    }

                    foreach (var view in selectedViews)
                    {
                        var param = view.get_Parameter(_idGuid);
                        if (param.IsReadOnly)
                        {
                            TaskDialog.Show("Ошибка", "Параметр OLP_Id заблокирован. Проверьте, что шаблон не заблокировал параметр");
                            return;
                        }

                        if (param.AsString().IsNullOrEmpty())
                            ids = idPalka;
                        else if (!param.AsString().Contains(idPalka))
                            ids = string.Join(";", [param.AsString(), idPalka]);

                        param.Set(ids);
                    }

                    _palka.get_Parameter(_idGuid).Set(string.Join(";", selectedViews.Select(v => v.Id.ToString()).OrderBy(id => id)));

                    t.Commit();
                }

                if (!ids.IsNullOrEmpty())
                {
                    var idParamId = RevitAPI.Document.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == _idGuid).Id;
                    RebarMethods.UpdateElements(RevitAPI.Document, idParamId, ids);
                }

                tGroup.Assimilate();
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