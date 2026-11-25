using Autodesk.Revit.DB;
using BIMPlugins.Classes;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Views.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;

namespace BIMPlugins.Views.WPF
{
    public enum ValuesScope { WholeModel, ActiveView }

    public enum ColoringArea { SurfacesAndLines, LinesOnly, SurfacesOnly }

    public partial class ColourFilterVM : ObservableObject
    {
        private const string Title = "Цветные фильтры";

        // The choices of the last window are kept for the next one.
        private static ValuesScope _lastScope = ValuesScope.WholeModel;
        private static ColoringArea _lastColoringArea = ColoringArea.SurfacesAndLines;
        private static readonly ColorDialog _colorDialog = new ColorDialog { FullOpen = true };

        [ObservableProperty] private string _filter;
        [ObservableProperty] private bool _hideUnchecked;
        [ObservableProperty] private List<string> _parameters = [];
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private string _selectedParameterName;
        [ObservableProperty] private ValuesScope _selectedScope = _lastScope;
        [ObservableProperty] private ColoringArea _selectedColoringArea = _lastColoringArea;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private List<ValueItem> _values = [];

        private readonly Document _doc;
        private readonly List<CategoryItem> _selectedCategories = [];
        private Dictionary<string, ElementId> _parameterIds = [];

        public ICollectionView Categories { get; }

        public IReadOnlyList<Option<ValuesScope>> Scopes { get; } =
        [
            new(ValuesScope.WholeModel, "Со всей модели"),
            new(ValuesScope.ActiveView, "С текущего вида")
        ];

        public IReadOnlyList<Option<ColoringArea>> ColoringAreas { get; } =
        [
            new(ColoringArea.SurfacesAndLines, "Поверхности и линии"),
            new(ColoringArea.LinesOnly, "Только линии"),
            new(ColoringArea.SurfacesOnly, "Только поверхности")
        ];

        public ColourFilterVM()
        {
            _doc = RevitAPI.Document;

            var categoryIds = _doc.Settings.Categories
                .Cast<Category>()
#if R2020_OR_GREATER
                .Where(c => c.IsVisibleInUI)
#endif
                .Select(c => c.Id)
                .ToList();

            var categories = ParameterFilterUtilities
                .RemoveUnfilterableCategories(categoryIds)
                .Select(id => new CategoryItem(_doc, id))
                .OrderBy(c => c.Name, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();

            Categories = CollectionViewSource.GetDefaultView(categories);
            Categories.Filter = item =>
            {
                var categoryItem = (CategoryItem)item;

                return (Filter.IsNullOrEmpty() || categoryItem.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase))
                    && (!HideUnchecked || categoryItem.IsSelected);
            };
        }

        partial void OnFilterChanged(string value) => Categories.Refresh();
        partial void OnHideUncheckedChanged(bool value) => Categories.Refresh();
        partial void OnSelectedParameterNameChanged(string value) => LoadValues();

        partial void OnSelectedScopeChanged(ValuesScope value)
        {
            _lastScope = value;
            LoadValues();
        }

        partial void OnSelectedColoringAreaChanged(ColoringArea value) => _lastColoringArea = value;

        [RelayCommand]
        private void CategoryChecked(CategoryItem categoryItem)
        {
            if (categoryItem.IsSelected)
                _selectedCategories.Add(categoryItem);
            else
                _selectedCategories.Remove(categoryItem);

            LoadParameters();
        }

        [RelayCommand]
        private void SelectVisible()
        {
            var visibleCategoryIds = new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                .WhereElementIsNotElementType()
                .Where(e => e.Category?.CategoryType == CategoryType.Model)
                .Select(e => e.Category.Id)
                .ToHashSet();

            // Categories already ticked are not added a second time.
            foreach (var categoryItem in Categories.OfType<CategoryItem>().Where(c => !c.IsSelected && visibleCategoryIds.Contains(c.Id)))
            {
                categoryItem.IsSelected = true;
                _selectedCategories.Add(categoryItem);
            }

            // The parameters common to the categories are asked from Revit once, not for each category.
            LoadParameters();
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var categoryItem in _selectedCategories)
                categoryItem.IsSelected = false;

            _selectedCategories.Clear();
            LoadParameters();
        }

        [RelayCommand]
        private void SetRandomColors()
        {
            foreach (var valueItem in Values)
                valueItem.Color = ViewGraphics.NextDistinctColor();
        }

        [RelayCommand]
        private void ChangeFilterColor(ValueItem valueItem)
        {
            _colorDialog.Color = System.Drawing.Color.FromArgb(valueItem.Color.Red, valueItem.Color.Green, valueItem.Color.Blue);

            if (_colorDialog.ShowDialog() == DialogResult.OK)
                valueItem.Color = new Color(_colorDialog.Color.R, _colorDialog.Color.G, _colorDialog.Color.B);
        }

        // The parameters of all the ticked categories; the chosen one stays chosen if they still have it.
        private void LoadParameters()
        {
            var selectedParameterName = SelectedParameterName;

            _parameterIds = _selectedCategories.Count == 0
                ? []
                : ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, _selectedCategories.Select(c => c.Id).ToList())
                    .GroupBy(GetParameterName)
                    .ToDictionary(g => g.Key, g => g.First());

            Parameters = _parameterIds.Keys
                .OrderBy(name => name, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();

            // With the same parameter the values still change: they come from other categories now.
            var parameterName = Parameters.FirstOrDefault(p => p == selectedParameterName);
            if (parameterName == SelectedParameterName)
                LoadValues();
            else
                SelectedParameterName = parameterName;
        }

        private string GetParameterName(ElementId parameterId) =>
            parameterId.ToElement<ParameterElement>(_doc)?.Name ?? LabelUtils.GetLabelFor((BuiltInParameter)parameterId.GetValue());

        private void LoadValues()
        {
            // The combo box is editable: a typed name may be no parameter at all.
            if (_selectedCategories.Count == 0 || SelectedParameterName == null || !_parameterIds.TryGetValue(SelectedParameterName, out var parameterId))
            {
                Values = [];
                return;
            }

            var categoriesFilter = new ElementMulticategoryFilter(_selectedCategories.Select(c => c.Id).ToList());

            var elements = SelectedScope == ValuesScope.WholeModel
                ? _doc.ToElements(categoriesFilter)
                : new FilteredElementCollector(_doc, _doc.ActiveView.Id).WherePasses(categoriesFilter).WhereElementIsNotElementType().ToElements();

            // The value is taken from the element, or from its type if the element has none; each type is read once.
            var typeParameters = new Dictionary<ElementId, Parameter>();
            var values = new Dictionary<string, ValueItem>();

            foreach (var element in elements)
            {
                var parameter = element.ToParameter(parameterId);
                if (parameter == null || !parameter.HasValue)
                {
                    var typeId = element.GetTypeId();
                    if (!typeParameters.TryGetValue(typeId, out parameter))
                    {
                        parameter = typeId.ToElement(_doc)?.ToParameter(parameterId);
                        typeParameters[typeId] = parameter;
                    }
                }

                if (parameter == null || !parameter.HasValue)
                    continue;

                var text = parameter.AsString().IsNullOrEmpty() ? parameter.AsValueString() : parameter.AsString();
                if (text != null && !values.ContainsKey(text))
                    values[text] = new ValueItem(parameter, text, ViewGraphics.NextDistinctColor());
            }

            Values = values.Values
                .OrderBy(v => v.ValueString, NaturalComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            var patternId = ViewGraphics.GetSolidFillPatternId(_doc);
            if (patternId == null)
            {
                MessageWindow.ShowMessage("В проекте нет образца сплошной заливки!", Title, icon: MessageBoxImage.Warning);
                return;
            }

            RaiseCloseRequest();

            var parameterId = _parameterIds[SelectedParameterName];
            var categoryIds = _selectedCategories.Select(c => c.Id).ToList();
            var filters = _doc.ToElements<ParameterFilterElement>();

            // A view under a template is coloured through the template.
            var activeView = _doc.ActiveView;
            var targetView = activeView.ViewTemplateId == ElementId.InvalidElementId
                ? activeView
                : activeView.ViewTemplateId.ToElement<View>(_doc);

            using (Transaction t = new Transaction(_doc, "Цветные фильтры"))
            {
                t.Start();

                foreach (var valueItem in Values)
                {
                    var filterName = ToValidFilterName($"BIMPlugins_{SelectedParameterName}_{valueItem.ValueString}");

                    var filterElement = filters.FirstOrDefault(f => f.Name == filterName);
                    if (filterElement != null)
                    {
                        filterElement.SetCategories(categoryIds);
                    }
                    else
                    {
                        var rule = valueItem.Value switch
                        {
                            ElementId id => parameterId.CreateEqualsFilter(id),
                            int intValue => parameterId.CreateEqualsFilter(intValue),
                            double doubleValue => parameterId.CreateEqualsFilter(doubleValue),
                            _ => parameterId.CreateEqualsFilter(valueItem.Value.ToString())
                        };

                        filterElement = ParameterFilterElement.Create(_doc, filterName, categoryIds, rule);
                    }

                    if (!targetView.IsFilterApplied(filterElement.Id))
                        targetView.AddFilter(filterElement.Id);

                    targetView.SetFilterOverrides(filterElement.Id, GetOverrides(valueItem.Color, patternId));
                }

                t.Commit();
            }
        }

        private bool CanRun() => SelectedParameterName != null && Values.Count != 0;

        // Revit does not take these characters in the name of a filter.
        private static string ToValidFilterName(string name) =>
            Regex.Replace(name, @"[\\:\{\}\[\]\|;<>\?`~]", "_");

        private OverrideGraphicSettings GetOverrides(Color color, ElementId patternId)
        {
            var overrides = new OverrideGraphicSettings();

            if (SelectedColoringArea != ColoringArea.LinesOnly)
            {
                overrides.SetCutForegroundPatternColor(color);
                overrides.SetCutForegroundPatternId(patternId);
                overrides.SetCutBackgroundPatternColor(color);
                overrides.SetCutBackgroundPatternId(patternId);

                overrides.SetSurfaceForegroundPatternColor(color);
                overrides.SetSurfaceForegroundPatternId(patternId);
                overrides.SetSurfaceBackgroundPatternColor(color);
                overrides.SetSurfaceBackgroundPatternId(patternId);
            }

            if (SelectedColoringArea != ColoringArea.SurfacesOnly)
            {
                overrides.SetProjectionLineColor(color);
                overrides.SetCutLineColor(color);
            }

            return overrides;
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }

    public partial class CategoryItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        public CategoryItem(Document doc, ElementId id)
        {
            Id = id;
#if R2020_OR_GREATER
            Name = LabelUtils.GetLabelFor((BuiltInCategory)id.GetValue());
#else
            Name = Category.GetCategory(doc, (BuiltInCategory)id.GetValue()).Name;
#endif
        }

        public ElementId Id { get; }
        public string Name { get; }
    }

    public partial class ValueItem(Parameter parameter, string value, Color color) : ObservableObject
    {
        [ObservableProperty] private Color _color = color;

        public string ValueString { get; } = value;
        public object Value { get; } = parameter.GetValue();
    }
}
