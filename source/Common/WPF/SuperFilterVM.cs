using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace BIMPlugins.Common.WPF
{
    public partial class SuperFilterVM : ObservableObject
    {
        [ObservableProperty] private ICollectionView _categories;
        [ObservableProperty] private ICollectionView _selectedCategories;
        [ObservableProperty] private string _filterCategory;
        [ObservableProperty] private string _filterParameter;
        [ObservableProperty] private FilterRule _selectedRule = FilterRule.Or;
        [ObservableProperty] private SelectionScope _selectedScope;

        private readonly UIDocument _uiDoc;
        private List<CategoryItem> _categoryItems = [];
        private List<ElementId> _elementIds = [];

        public IReadOnlyList<Option<SelectionScope>> Scopes { get; } =
        [
            new(SelectionScope.WholeModel, "Выборка по всем элементам"),
            new(SelectionScope.ActiveView, "Выборка по текущему виду"),
            new(SelectionScope.Selection, "Выборка по выделенным элементам")
        ];

        public IReadOnlyList<Option<FilterRule>> Rules { get; } =
        [
            new(FilterRule.And, "И"),
            new(FilterRule.Or, "Или")
        ];

        public SuperFilterVM()
        {
            _uiDoc = RevitAPI.UIDocument;

            // The field, not the property: the property would load the categories a second time.
            _selectedScope = _uiDoc.Selection.GetElementIds().Count != 0
                ? SelectionScope.Selection
                : SelectionScope.WholeModel;

            LoadCategories();
        }

        partial void OnSelectedScopeChanged(SelectionScope value) => LoadCategories();

        partial void OnFilterCategoryChanged(string value) => Categories.Refresh();

        partial void OnFilterParameterChanged(string value)
        {
            foreach (var categoryItem in _categoryItems)
                categoryItem.ParametersView.Refresh();
        }

        private void LoadCategories()
        {
            var doc = _uiDoc.Document;

            var elements = SelectedScope == SelectionScope.Selection
                ? _uiDoc.ToSelectedElements()
                    .Where(e => e.Category != null)
                    .ToList()
                : (SelectedScope == SelectionScope.WholeModel
                        ? new FilteredElementCollector(doc)
                        : new FilteredElementCollector(doc, doc.ActiveView.Id))
                    .WhereElementIsNotElementType()
                    .Where(e => e.Category?.CategoryType is CategoryType.Model or CategoryType.Annotation)
                    .ToList();

            _elementIds = elements.Select(e => e.Id).ToList();

            // Elements are grouped by category once, so that expanding a category asks Revit for nothing more.
            _categoryItems = elements
                .GroupBy(e => e.Category.Id)
                .Select(g => new CategoryItem(g.First().Category, g.ToList(), IsParameterVisible))
                .OrderBy(c => c.Name, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();

            Categories = FilteredView.Create(_categoryItems, c => IsCategoryVisible((CategoryItem)c));
            SelectedCategories = FilteredView.Create(_categoryItems, c => ((CategoryItem)c).HasSelection, nameof(CategoryItem.HasSelection));
        }

        private bool IsCategoryVisible(CategoryItem categoryItem) =>
            FilterCategory.IsNullOrEmpty() || categoryItem.Name.Contains(FilterCategory, StringComparison.OrdinalIgnoreCase);

        private bool IsParameterVisible(ParameterItem parameterItem) =>
            parameterItem.IsPlaceholder
            || parameterItem.IsAll
            || FilterParameter.IsNullOrEmpty()
            || parameterItem.Name.Contains(FilterParameter, StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        private void Collapse()
        {
            foreach (var categoryItem in _categoryItems)
            {
                categoryItem.IsExpanded = false;
                foreach (var parameterItem in categoryItem.Parameters)
                    parameterItem.IsExpanded = false;
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var parameterItem in _categoryItems.SelectMany(c => c.Parameters).Where(p => p.IsSelected != false))
                parameterItem.IsSelected = false;
        }

        [RelayCommand]
        private void Run(FilterAction action)
        {
            RaiseCloseRequest();

            var filteredIds = GetFilteredElementIds();

            switch (action)
            {
                case FilterAction.Select:
                    _uiDoc.Selection.SetElementIds(filteredIds);
                    break;

                case FilterAction.Exclude:
                    _uiDoc.Selection.SetElementIds(_elementIds.Where(id => !filteredIds.Contains(id)).ToList());
                    break;

                case FilterAction.Isolate when filteredIds.Count != 0:
                    ChangeActiveView("Изолировать выбранные элементы", view => view.IsolateElementsTemporary(filteredIds));
                    break;

                case FilterAction.Hide when filteredIds.Count != 0:
                    ChangeActiveView("Скрыть выбранные элементы", view => view.HideElementsTemporary(filteredIds));
                    break;
            }
        }

        private HashSet<ElementId> GetFilteredElementIds()
        {
            var filteredIds = new HashSet<ElementId>();

            foreach (var categoryItem in _categoryItems.Where(c => c.HasSelection))
            {
                var parameters = categoryItem.Parameters.Where(p => p.IsSelected != false).ToList();
                var valueParameters = parameters.Where(p => !p.IsAll).ToList();

                if (valueParameters.Count == 0 || (SelectedRule == FilterRule.Or && parameters.Any(p => p.IsAll)))
                {
                    filteredIds.UnionWith(categoryItem.Elements.Select(e => e.Id));
                }
                else if (SelectedRule == FilterRule.Or)
                {
                    foreach (var parameterItem in valueParameters)
                        filteredIds.UnionWith(parameterItem.SelectedElementIds);
                }
                else
                {
                    var commonIds = new HashSet<ElementId>(valueParameters[0].SelectedElementIds);
                    foreach (var parameterItem in valueParameters.Skip(1))
                        commonIds.IntersectWith(parameterItem.SelectedElementIds);

                    filteredIds.UnionWith(commonIds);
                }
            }

            return filteredIds;
        }

        private void ChangeActiveView(string transactionName, Action<View> change)
        {
            var doc = _uiDoc.Document;

            using (Transaction t = new Transaction(doc, transactionName))
            {
                t.Start();
                change(doc.ActiveView);
                t.Commit();
            }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
