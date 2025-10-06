using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Views.WPF
{
    public partial class ColourFilterViewModel : ObservableObject
    {
        [ObservableProperty] private string _filter;
        [ObservableProperty] private bool _hideUnchecked = false;
        [ObservableProperty] private ObservableCollection<string> _parameters = [];
        [ObservableProperty] private string _selectedParameterName;
        [ObservableProperty] static private string _selectedOption = "Со всей модели";
        [ObservableProperty] static private string _selectedColoringOption = "Поверхности и линии";

        [ObservableProperty] private ObservableCollection<ValueItem> _values = [];

        private readonly Dictionary<string, ElementId> _parametersDict = [];
        private readonly ObservableCollection<CategoryItem> _selectedCategories = [];
        private const double GoldenRatioConjugate = 0.618033988749895;
        private static double _currentHue = 0;
        private static readonly ColorDialog _colorDialog = new ColorDialog() { FullOpen = true};

        partial void OnFilterChanged(string value)
        {
            Categories.Refresh();
        }
        partial void OnHideUncheckedChanged(bool value)
        {
            Categories.Refresh();
        }

        partial void OnSelectedParameterNameChanged(string value)
        {
            GetValues();
        }
        partial void OnSelectedOptionChanged(string value)
        {
            GetValues();
        }

        public ICollectionView Categories { get; }
        public List<string> SelectionOptions { get; } = ["Со всей модели", "С текущего вида"];
        public List<string> ColoringOptions { get; } = ["Поверхности и линии", "Только линии", "Только поверхности"];

        public ColourFilterViewModel()
        {
            var categories = RevitAPI.Document.Settings.Categories
                .Cast<Category>()
#if R2020_OR_GREATER
                .Where(c => c.IsVisibleInUI)
#endif
                .Select(c => c.Id)
                .ToList();

            var filteredCategories = ParameterFilterUtilities
                .RemoveUnfilterableCategories(categories)
                .Select(id => new CategoryItem(id))
                .OrderBy(c => c.Name)
                .ToList();

            Categories = CollectionViewSource.GetDefaultView(filteredCategories);
            Categories.Filter = item =>
            {
                var categoryItem = (CategoryItem)item;

                bool nameFilter = Filter.IsNullOrEmpty() || categoryItem.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase);
                bool selectionFilter = !HideUnchecked || categoryItem.IsSelected;

                return nameFilter && selectionFilter;
            };
        }


        [RelayCommand]
        private void CategoryChecked(CategoryItem categoryItem)
        {
            var selectedParameter = SelectedParameterName;
            Parameters.Clear();

            if (categoryItem.IsSelected)
                _selectedCategories.Add(categoryItem);
            else
                _selectedCategories.Remove(categoryItem);

            if (_selectedCategories.Count == 0)
                return;

            var ids = ParameterFilterUtilities.GetFilterableParametersInCommon(RevitAPI.Document, _selectedCategories.Select(c => c.Id).ToList());
            foreach (var id in ids)
            {
                var parameter = id.ToElement<ParameterElement>();
                string parameterName = parameter?.Name ?? LabelUtils.GetLabelFor((BuiltInParameter)id.GetValue());

                Parameters.Add(parameterName);
                _parametersDict[parameterName] = id;
            }
            
            Parameters = new (Parameters.OrderBy(p => p).ToList());
            SelectedParameterName = Parameters.FirstOrDefault(p => p == selectedParameter);
        }

        [RelayCommand]
        private void SelectVisible()
        {
            var visibleCategoriesNames = new FilteredElementCollector(RevitAPI.Document, RevitAPI.Document.ActiveView.Id)
                .Where(c => c.Category != null)
                .Where(c => c.Category.CategoryType == CategoryType.Model)
                .Select(c => c.Category.Name)
                .ToHashSet();

            foreach (var categoryItem in Categories.OfType<CategoryItem>().ToList())
            {
                if (visibleCategoriesNames.Contains(categoryItem.Name))
                {
                    categoryItem.IsSelected = true;
                    CategoryChecked(categoryItem);
                }
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var categoryItem in _selectedCategories)
                categoryItem.IsSelected = false;

            _selectedCategories.Clear();
            Parameters.Clear();
        }

        [RelayCommand]
        private void SetRandomColors()
        {
            foreach (var value in Values)
            {
                value.Color = GenerateDistinctColor();
            }
        }

        [RelayCommand]
        private void ChangeFilterColor(ValueItem valueItem)
        {
            _colorDialog.Color = System.Drawing.Color.FromArgb(valueItem.Color.Red, valueItem.Color.Green, valueItem.Color.Blue);

            if (_colorDialog.ShowDialog() == DialogResult.OK)
            {
                valueItem.Color = new (_colorDialog.Color.R, _colorDialog.Color.G, _colorDialog.Color.B);
            }
        }

        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();

            var patternId = new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(FillPatternElement))
                .FirstOrDefault(e => e.Name == "<Сплошная заливка>")
                .Id;

            var filters = new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .ToList();

            using (Transaction t = new Transaction(RevitAPI.Document, "Цветные фильтры"))
            {
                t.Start();

                foreach (var valueItem in Values)
                {
                    var parameterId = _parametersDict[SelectedParameterName];
                    var value = valueItem.Value;

                    var paramFilter = value switch
                    {
                        ElementId id => parameterId.CreateEqualsFilter(id),
                        int intValue => parameterId.CreateEqualsFilter(intValue),
                        double doubleValue => parameterId.CreateEqualsFilter(doubleValue),
                        _ => parameterId.CreateEqualsFilter(value.ToString())
                    };

                    var parameterFilterElement = filters.FirstOrDefault(f => f.Name == $"SEPlugins_{SelectedParameterName}_{valueItem.ValueString}");
                    if (parameterFilterElement != null)
                    {
                        parameterFilterElement.SetCategories(_selectedCategories.Select(c => c.Id).ToList());
                    }
                    else
                    {
                        parameterFilterElement = ParameterFilterElement.Create(
                            RevitAPI.Document,
                            $"SEPlugins_{SelectedParameterName}_{valueItem.ValueString}",
                            _selectedCategories.Select(c => c.Id).ToList(),
                            paramFilter
                        );
                    }

                    var activeView = RevitAPI.ActiveView;
                    if (activeView.ViewTemplateId == ElementId.InvalidElementId)
                    {
                        if (!activeView.IsFilterApplied(parameterFilterElement.Id))
                        {
                            activeView.AddFilter(parameterFilterElement.Id);
                        }

                        activeView.SetFilterOverrides(parameterFilterElement.Id, SetPatternColor(valueItem.Color, patternId));
                    }
                    else
                    {
                        var template = activeView.ViewTemplateId.ToElement<Autodesk.Revit.DB.View>();
                        if (!template.IsFilterApplied(parameterFilterElement.Id))
                        {
                            template.AddFilter(parameterFilterElement.Id);
                        }

                        template.SetFilterOverrides(parameterFilterElement.Id, SetPatternColor(valueItem.Color, patternId));
                    }
                }

                t.Commit();
            }
        }

        private void GetValues()
        {
            var multicategoryFilter = new ElementMulticategoryFilter(_selectedCategories.Select(c => (BuiltInCategory)c.Id.GetValue()).ToList());

            var elements = SelectedOption == "Со всей модели"
                ? new FilteredElementCollector(RevitAPI.Document).WherePasses(multicategoryFilter).WhereElementIsNotElementType().ToList()
                : new FilteredElementCollector(RevitAPI.Document, RevitAPI.ActiveView.Id).WherePasses(multicategoryFilter).WhereElementIsNotElementType().ToList();

            Values.Clear();
            foreach (var element in elements)
            {
                var parameter = SelectNonNullParameter(element, SelectedParameterName);
                if (parameter == null) continue;

                var value = parameter.AsString().IsNullOrEmpty() ? parameter.AsValueString() : parameter.AsString();

                if (!Values.Select(v => v.ValueString).Contains(value))
                    Values.Add(new ValueItem(parameter, value, GenerateDistinctColor()));
            }

            Values = new (Values.OrderBy(v => v.ValueString, new NaturalComparer()).ToList());
        }
        private Parameter SelectNonNullParameter(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue)
            {
                return param;
            }

            Parameter paramType = element.ToElementType().LookupParameter(parameterName);
            if (paramType != null && paramType.HasValue)
            {
                return paramType;
            }

            return null;
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

        private OverrideGraphicSettings SetPatternColor(Color color, ElementId patternId)
        {
            var graphicSettings = new OverrideGraphicSettings();

            if (SelectedColoringOption != "Только линии")
            {
                graphicSettings.SetCutForegroundPatternColor(color);
                graphicSettings.SetCutForegroundPatternId(patternId);
                graphicSettings.SetCutBackgroundPatternColor(color);
                graphicSettings.SetCutBackgroundPatternId(patternId);

                graphicSettings.SetSurfaceForegroundPatternColor(color);
                graphicSettings.SetSurfaceForegroundPatternId(patternId);
                graphicSettings.SetSurfaceBackgroundPatternColor(color);
                graphicSettings.SetSurfaceBackgroundPatternId(patternId);
            }

            if (SelectedColoringOption != "Только поверхности")
            {
                graphicSettings.SetProjectionLineColor(color);
                graphicSettings.SetCutLineColor(color);
            }

            return graphicSettings;
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }

    public partial class CategoryItem(ElementId id) : ObservableObject
    {
        [ObservableProperty] private bool _isSelected = false;

        public ElementId Id { get; set; } = id;
#if R2020_OR_GREATER
        public string Name { get; set; } = LabelUtils.GetLabelFor((BuiltInCategory)id.GetValue());
#else
        public string Name { get; set; } = Category.GetCategory(RevitAPI.Document, (BuiltInCategory)id.GetValue()).Name;
#endif
    }

    public partial class ValueItem(Parameter parameter, string value, Color color) : ObservableObject
    {
        [ObservableProperty] private Color _color = color;

        public string ValueString { get; set; } = value;
        public object Value { get; set; } = parameter.GetValue();
    }
}