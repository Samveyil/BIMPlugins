using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.ClashViewer.WPF
{
    public partial class UserFilterViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<CategoryItem> _categoryItems = [];
        [ObservableProperty] private string _filter;
        [ObservableProperty] private ObservableCollection<CategoryItem> _selectedCategoryItems = [];
        [ObservableProperty] private string _selectedRule = "Или";

        partial void OnFilterChanged(string value)
        {
            foreach (var categoryItem in CategoryItems.Where(c => c.Parameters != null))
            {
                categoryItem.Parameters.Refresh();
            }
        }

        private readonly List<Element> _elements;

        public List<string> FilterRules { get; } = ["И", "Или"];
#if !R2024_OR_GREATER
        public List<int> FilteredIds { get; private set; } = [];
#else
        public List<long> FilteredIds { get; private set; } = [];
#endif
        public bool IsCommandExecuted { get; private set; } = false;
        public string SelectedFilterRules { get; private set; }

        public UserFilterViewModel(List<Element> elements)
        {
            _elements = elements;

            var uniqueCategories = elements
                .GroupBy(e => e.Category.Name)
                .Select(g => g.First())
                .OrderBy(e => e.Category.Name)
                .Select(e => e.Category)
                .ToList();

            foreach (var category in uniqueCategories)
            {                
                CategoryItems.Add(new CategoryItem(category));
            }
        }

        [RelayCommand]
        private void TreeItemClicked(object item)
        {
            if (item is CategoryItem categoryItem)
            {
                if (categoryItem.Parameters != null)
                    return;

                var elements = new FilteredElementCollector(RevitAPI.Document, _elements.Select(e => e.Id).ToList())
                    .OfCategory(categoryItem.BuiltInCategory)
                    .WhereElementIsNotElementType()
                    .ToList();

                var uniqueParameters = elements
                    .SelectMany(e => e.Parameters.Cast<Parameter>())
                    .GroupBy(p => p.Definition.Name)
                    .Select(g => g.First())
                    .Select(p => new ParameterItem(p) { Parent = categoryItem });

                var uniqueTypeParameters = elements.Select(e => e.ToElementType())
                    .SelectMany(e => e.Parameters.Cast<Parameter>())
                    .GroupBy(p => p.Definition.Name)
                    .Select(g => g.First())
                    .Select(p => new ParameterItem(p) { Parent = categoryItem, IsTypeParameter = true });

                var allParameters = uniqueParameters
                    .Concat(uniqueTypeParameters)
                    .OrderBy(p => p.Name)
                    .ThenByDescending(p => p.IsTypeParameter)
                    .ToList();

                allParameters.Insert(0, new ParameterItem() { Parent = categoryItem });

                categoryItem.Parameters = CollectionViewSource.GetDefaultView(allParameters);

                categoryItem.Parameters.Filter = item =>
                {
                    if (Filter.IsNullOrEmpty())
                        return true;

                    var parameterItem = (ParameterItem)item;
                    return parameterItem.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase);
                };
            }
            else if (item is ParameterItem parameterItem)
            {
                if (parameterItem.Values.Count != 0 || parameterItem.Name == "Все")
                    return;

                var elements = new FilteredElementCollector(RevitAPI.Document, _elements.Select(e => e.Id).ToList())
                    .OfCategory(parameterItem.Parent.BuiltInCategory)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var valuesDict = new Dictionary<string, List<Element>>();
                foreach (var element in elements)
                {
                    var parameter = parameterItem.IsTypeParameter
                        ? element.ToElementType().Parameters.Cast<Parameter>().FirstOrDefault(p => p.Id.ToString() == parameterItem.Id.ToString())
                        : element.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Id.ToString() == parameterItem.Id.ToString());
                    
                    if (parameter == null)
                        continue;

                    var value = parameter.HasValue
                        ? parameter.StorageType == StorageType.String
                            ? parameter.AsString()
                            : parameter.AsValueString()
                        : "[Не задан]";

                    if (value.IsNullOrEmpty())
                        value = "[Нет значения]";

                    if (!valuesDict.ContainsKey(value))
                        valuesDict[value] = [];

                    valuesDict[value].Add(element);
                }

                var sortedValues = valuesDict
                    .OrderBy(kvp => kvp.Key == "[Не задан]" ? 0 : 1)
                    .ThenBy(kvp => kvp.Key == "[Нет значения]" ? 0 : 1)
                    .ThenBy(kvp => kvp.Key, new NaturalComparer())
                    .ToList();

                foreach (var kvp in sortedValues)
                {
                    parameterItem.Values.Add(new ValueItem(kvp.Key) { Parent = parameterItem, Elements = kvp.Value });
                }
            }
        }

        [RelayCommand]
        private void Collapse()
        {
            foreach (var categoryItem in CategoryItems.Where(c => c.Parameters != null))
            {
                categoryItem.IsExpanded = false;
                categoryItem.Parameters.Cast<ParameterItem>()
                    .Where(p => p.Values.Count > 0)
                    .ToList()
                    .ForEach(p => p.IsExpanded = false);
            }
        }

        [RelayCommand]
        private void ParameterItemChecked(ParameterItem parameterItem)
        {
            if (parameterItem.IsSelected == true)
            {
                if (parameterItem.Name == "Все")
                {
                    var categoryItem = parameterItem.Parent;

                    var selectedCategoryItem = SelectedCategoryItems.FirstOrDefault(c => c.Name == categoryItem.Name);
                    if (selectedCategoryItem != null)
                    {
                        var newParameterItem = new ParameterItem() { Parent = selectedCategoryItem, IsSelected=true };
                        
                        var parameters = selectedCategoryItem.Parameters.SourceCollection as ObservableCollection<ParameterItem>;
                        parameters.Add(newParameterItem);
                    }
                    else
                    {
                        var newCategoryItem = new CategoryItem(categoryItem);
                        
                        var newParameterItem = new ParameterItem() { Parent = newCategoryItem, IsSelected = true };
                        newCategoryItem.Parameters = CollectionViewSource.GetDefaultView(new ObservableCollection<ParameterItem> { newParameterItem });
                        
                        SelectedCategoryItems.Add(newCategoryItem);
                    }
                }
                else
                {
                    foreach (var valueItem in parameterItem.Values)
                        AddValueItemToSelection(valueItem);
                }
            }
            else if (parameterItem.IsSelected == false)
            {
                if (parameterItem.Name == "Все")
                {
                    var categoryItem = parameterItem.Parent;
                    
                    var selectedCategoryItem = SelectedCategoryItems.FirstOrDefault(c => c.Name == categoryItem.Name);
                    if (selectedCategoryItem == null) return;

                    var selectedParameterItem = selectedCategoryItem.Parameters
                        .OfType<ParameterItem>()
                        .FirstOrDefault(p => p.Name == parameterItem.Name);
                    if (selectedParameterItem == null) return;

                    (selectedCategoryItem.Parameters.SourceCollection as ObservableCollection<ParameterItem>).Remove(selectedParameterItem);

                    if (!selectedCategoryItem.Parameters.OfType<ParameterItem>().Any())
                    {
                        SelectedCategoryItems.Remove(selectedCategoryItem);
                    }
                }
                else
                {
                    foreach (var valueItem in parameterItem.Values.ToList())
                        RemoveValueItemFromSelection(valueItem);
                }
            }
        }

        [RelayCommand]
        private void ValueItemChecked(ValueItem valueItem)
        {
            if (valueItem.IsSelected)
                AddValueItemToSelection(valueItem);
            else
                RemoveValueItemFromSelection(valueItem);
        }

        [RelayCommand]
        private void SelectedParameterItemChecked(ParameterItem selectedParameterItem)
        {
            var selectedCategoryItem = selectedParameterItem.Parent;
            (selectedCategoryItem.Parameters.SourceCollection as ObservableCollection<ParameterItem>).Remove(selectedParameterItem);

            if (!selectedCategoryItem.Parameters.OfType<ParameterItem>().Any())
            {
                SelectedCategoryItems.Remove(selectedCategoryItem);
            }

            var parameterItem = CategoryItems.FirstOrDefault(c => c.Name == selectedCategoryItem.Name)
                .Parameters
                .OfType<ParameterItem>()
                .FirstOrDefault(p => p.Name == selectedParameterItem.Name);

            parameterItem.IsSelected = false;
        }

        [RelayCommand]
        private void SelectedValueItemChecked(ValueItem selectedValueItem)
        {
            var selectedParameterItem = selectedValueItem.Parent;
            var selectedCategoryItem = selectedParameterItem.Parent;

            selectedParameterItem.Values.Remove(selectedValueItem);

            if (!selectedParameterItem.Values.Any())
            {
                (selectedCategoryItem.Parameters.SourceCollection as ObservableCollection<ParameterItem>).Remove(selectedParameterItem);

                if (!selectedCategoryItem.Parameters.OfType<ParameterItem>().Any())
                {
                    SelectedCategoryItems.Remove(selectedCategoryItem);
                }
            }

            CategoryItems.FirstOrDefault(c => c.Name == selectedCategoryItem.Name)
                .Parameters
                .OfType<ParameterItem>()
                .FirstOrDefault(p => p.Name == selectedParameterItem.Name)
                .Values
                .FirstOrDefault(v => v.Value == selectedValueItem.Value)
                .IsSelected = false;
        }

        [RelayCommand]
        private void RemoveAll()
        {
            foreach (var categoryItem in SelectedCategoryItems.ToList())
            {
                categoryItem.Parameters
                    .OfType<ParameterItem>()
                    .ToList()
                    .ForEach(p => SelectedParameterItemChecked(p));
            }
        }

        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();
            IsCommandExecuted = true;

            FilteredIds.Clear();

            var filterConditions = new List<string>();
            foreach (var categoryItem in SelectedCategoryItems)
            {
                var categoryConditions = new List<string>();

                if (SelectedRule == "Или" || categoryItem.Parameters.OfType<ParameterItem>().Count() == 1)
                {
                    if (categoryItem.Parameters.OfType<ParameterItem>().FirstOrDefault(p => p.Name == "Все") != null)
                    {
                        categoryConditions.Add("Все");

                        FilteredIds.AddRange(new FilteredElementCollector(RevitAPI.Document, _elements.Select(e => e.Id).ToList())
                            .OfCategory(categoryItem.BuiltInCategory)
                            .WhereElementIsNotElementType()
                            .Select(e => e.Id.GetValue())
                            .ToList());
                    }
                    else
                    {
                        var parametersWithValues = new List<string>();

                        foreach (var parameter in categoryItem.Parameters.OfType<ParameterItem>().Where(p => p.Name != "Все"))
                        {
                            var values = parameter.Values.Select(v => v.Value).ToList();
                            parametersWithValues.Add($"{parameter.Name} = {string.Join(", ", values)}");

                            FilteredIds.AddRange(parameter.Values
                                .SelectMany(v => v.Elements)
                                .Select(v => v.Id.GetValue())
                                .ToHashSet());
                        }

                        categoryConditions.AddRange(parametersWithValues);
                    }
                }
                else
                {
                    var parametersWithValues = new List<string>();
#if !R2024_OR_GREATER
                    var allElementsGroups = new List<HashSet<int>>();
#else
                    var allElementsGroups = new List<HashSet<long>>();
#endif
                    foreach (var parameter in categoryItem.Parameters.OfType<ParameterItem>().Where(p => p.Name != "Все"))
                    {
                        var values = parameter.Values.Select(v => v.Value).ToList();
                        parametersWithValues.Add($"{parameter.Name} = {string.Join(", ", values)}");

                        allElementsGroups.Add(parameter.Values
                            .SelectMany(v => v.Elements)
                            .Select(e => e.Id.GetValue())
                            .ToHashSet());
                    }

                    if (allElementsGroups.Any())
                    {
                        var commonIds = allElementsGroups
                            .Skip(1)
                            .Aggregate(
#if !R2024_OR_GREATER
                                new HashSet<int>(allElementsGroups.First()),
#else
                                new HashSet<long>(allElementsGroups.First()),
#endif
                                (intersect, ids) => { intersect.IntersectWith(ids); return intersect; }
                            );

                        FilteredIds.AddRange(commonIds);
                    }

                    categoryConditions.Add($"И: {string.Join("; ", parametersWithValues)}");
                }

                filterConditions.Add($"{categoryItem.Name}: {string.Join("; ", categoryConditions)}");
            }

            SelectedFilterRules = string.Join("\n", filterConditions);
        }

        private void AddValueItemToSelection(ValueItem valueItem)
        {
            var categoryItem = valueItem.Parent.Parent;
            var parameterItem = valueItem.Parent;

            var selectedCategoryItem = SelectedCategoryItems.FirstOrDefault(c => c.Name == categoryItem.Name);
            if (selectedCategoryItem != null)
            {
                var selectedParameterItem = selectedCategoryItem.Parameters
                    .OfType<ParameterItem>()
                    .FirstOrDefault(p => p.Name == parameterItem.Name);

                if (selectedParameterItem != null)
                {
                    selectedParameterItem.Values.Add(new ValueItem(valueItem)
                    {
                        Parent = selectedParameterItem,
                        IsSelected = true
                    });
                }
                else
                {
                    var newParameterItem = new ParameterItem(parameterItem) { Parent = selectedCategoryItem };
                    newParameterItem.Values.Add(new ValueItem(valueItem) { Parent = newParameterItem, IsSelected = true });

                    var parameters = selectedCategoryItem.Parameters.SourceCollection as ObservableCollection<ParameterItem>;
                    parameters.Add(newParameterItem);
                }
            }
            else
            {
                var newCategoryItem = new CategoryItem(categoryItem);

                var newParameterItem = new ParameterItem(parameterItem) { Parent = newCategoryItem };
                newParameterItem.Values.Add(new ValueItem(valueItem) { Parent = newParameterItem, IsSelected = true });

                newCategoryItem.Parameters = CollectionViewSource.GetDefaultView(new ObservableCollection<ParameterItem> { newParameterItem });
                SelectedCategoryItems.Add(newCategoryItem);
            }
        }
        private void RemoveValueItemFromSelection(ValueItem valueItem)
        {
            var categoryItem = valueItem.Parent.Parent;
            var parameterItem = valueItem.Parent;

            var selectedCategoryItem = SelectedCategoryItems.FirstOrDefault(c => c.Name == categoryItem.Name);
            if (selectedCategoryItem == null) return;

            var selectedParameterItem = selectedCategoryItem.Parameters
                .OfType<ParameterItem>()
                .FirstOrDefault(p => p.Name == parameterItem.Name);
            if (selectedParameterItem == null) return;

            var valueToRemove = selectedParameterItem.Values.FirstOrDefault(v => v.Value == valueItem.Value);
            if (valueToRemove == null) return;

            selectedParameterItem.Values.Remove(valueToRemove);

            if (!selectedParameterItem.Values.Any())
            {
                (selectedCategoryItem.Parameters.SourceCollection as ObservableCollection<ParameterItem>).Remove(selectedParameterItem);

                if (!selectedCategoryItem.Parameters.OfType<ParameterItem>().Any())
                {
                    SelectedCategoryItems.Remove(selectedCategoryItem);
                }
            }
        }


        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }

    public partial class CategoryItem : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded = true;
        [ObservableProperty] ICollectionView _parameters;

        public CategoryItem(Category category)
        {
            Name = category.Name;
            BuiltInCategory = (BuiltInCategory)category.Id.GetValue();
        }
        public CategoryItem(CategoryItem other)
        {
            Name = other.Name;
            BuiltInCategory = other.BuiltInCategory;
        }

        public string Name { get; set; }
        public BuiltInCategory BuiltInCategory { get; set; }
    }

    public partial class ParameterItem : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded = true;
        [ObservableProperty] private bool? _isSelected = false;
        [ObservableProperty] private bool _isTypeParameter = false;
        [ObservableProperty] private ObservableCollection<ValueItem> _values = [];

        partial void OnIsSelectedChanged(bool? value)
        {
            if (value == true)
            {
                foreach (var item in Values)
                {
                    item.IsSelected = true;
                }
            }
            else if (value == false)
            {
                foreach (var item in Values)
                {
                    item.IsSelected = false;
                }
            }
        }

        public ParameterItem()
        {
            Name = "Все";
        }
        public ParameterItem(Parameter parameter)
        {
            Name = parameter.Definition.Name;
            Id = parameter.Id;
        }
        public ParameterItem(ParameterItem other)
        {
            Name = other.Name;
            Id = other.Id;
            IsTypeParameter = other.IsTypeParameter;
            IsSelected = true;
        }


        public CategoryItem Parent { get; set; }
        public string Name { get; set; }
        public ElementId Id { get; set; }
    }

    public partial class ValueItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected = false;

        partial void OnIsSelectedChanged(bool value)
        {
            if (value)
            {
                Parent.IsSelected = Parent.Values.All(s => s.IsSelected)
                    ? true
                    : null;
            }
            else
            {
                Parent.IsSelected = Parent.Values.Any(s => s.IsSelected)
                    ? null
                    : false;
            }
        }

        public ValueItem(string value)
        {
            Value = value;
        }
        public ValueItem(ValueItem other)
        {
            Value = other.Value;
            Elements = other.Elements;
        }

        public string Value { get; set; }
        public ParameterItem Parent { get; set; }
        public List<Element> Elements { get; set; }
    }
}