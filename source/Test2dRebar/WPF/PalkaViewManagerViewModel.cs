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
        private readonly List<Element> _palkas;
        private readonly List<string> _palkaIds;
        private readonly List<ViewSection> _views;
        private readonly List<ViewSection> _lastSelectedViews = [];   

        public PalkaViewManagerViewModel(List<ViewSection> views, List<Element> palkas)
        {
            _views = views;
            _palkas = palkas;

            _palkaIds = _palkas.Select(p => p.Id.ToString()).ToList();

            var viewItems = new List<ViewItem>();
            foreach (var view in _views.OrderBy(v => v.Title))
            {
                bool isSelected = false;

                var param = view.get_Parameter(_idGuid).AsString();
                if (!param.IsNullOrEmpty())
                {
                    var viewPalkaIds = param.Split(';').Select(s => s.Trim()).ToList();
                    isSelected = _palkaIds.Any(id => viewPalkaIds.Contains(id));
                }   

                var item = new ViewItem(view) { IsSelected = isSelected };
                viewItems.Add(item);

                if (isSelected)
                    _lastSelectedViews.Add(view);
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
        private void NewSection()
        {
            RaiseCloseRequest();

            RebarMethods.CreateViewSection(_palkas);
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

            var idParamId = RevitAPI.Document.ToElements<SharedParameterElement>().FirstOrDefault(p => p.GuidValue == _idGuid).Id;

            using (TransactionGroup tGroup = new TransactionGroup(RevitAPI.Document, "Присвоить палку к виду"))
            {
                tGroup.Start();

                using (Transaction t = new Transaction(RevitAPI.Document, "Удалить старые виды палки"))
                {
                    t.Start();

                    foreach (var view in _lastSelectedViews.Except(selectedViews).ToList())
                    {
                        var param = view.get_Parameter(_idGuid);
                        if (param.IsReadOnly)
                        {
                            TaskDialog.Show("Ошибка", "Параметр OLP_Id заблокирован. Проверьте, что шаблон вида не заблокировал параметр");
                            return;
                        }

                        if (param.AsString().IsNullOrEmpty())
                            continue;

                        var result = param.AsString();
                        foreach (var palka in _palkas)
                        {
                            if (result.Contains(palka.Id.ToString()))
                                result = result.Replace(palka.Id.ToString(), "");
                        }

                        param.Set(result.Trim(';').Replace(";;", ";"));
                    }

                    t.Commit();
                }

                foreach (var view in _lastSelectedViews.Except(selectedViews).ToList())
                {
                    RebarMethods.UpdateElements(RevitAPI.Document, idParamId, view);
                }

                using (Transaction t = new Transaction(RevitAPI.Document, "Присвоить новые виды палке"))
                {
                    t.Start();

                    foreach (var view in selectedViews)
                    {
                        var param = view.get_Parameter(_idGuid);
                        if (param.IsReadOnly)
                        {
                            TaskDialog.Show("Ошибка", "Параметр OLP_Id заблокирован. Проверьте, что шаблон не заблокировал параметр");
                            return;
                        }

                        var result = param.AsString();
                        if (result.IsNullOrEmpty())
                            result = string.Join(";", _palkaIds);
                        else
                        {
                            foreach (var palka in _palkas)
                            {
                                if (!result.Contains(palka.Id.ToString()))
                                    result = string.Join(";", [result, palka.Id.ToString()]);
                            }
                        }

                        param.Set(result);
                    }

                    foreach (var palka in _palkas)
                        palka.get_Parameter(_idGuid).Set(string.Join(";", selectedViews.Select(v => v.Id.ToString()).OrderBy(id => id)));

                    t.Commit();
                }

                if (selectedViews.Count != 0)
                    RebarMethods.UpdateElements(RevitAPI.Document, idParamId, selectedViews.First());

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