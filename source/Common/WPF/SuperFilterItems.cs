using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Comparers;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace BIMPlugins.Common.WPF
{
    public enum SelectionScope { WholeModel, ActiveView, Selection }

    public enum FilterRule { And, Or }

    public enum FilterAction { Select, Exclude, Isolate, Hide }

    /// <summary>A value for a combo box with the text it shows.</summary>
    public sealed class Option<T>(T value, string text)
    {
        public T Value { get; } = value;
        public string Text { get; } = text;
    }

    /// <summary>
    /// A category of the chosen elements. Its parameters load when it is expanded first. The right tree of the window
    /// shows the same items through <see cref="SelectedParametersView"/>, so a tick lives in one place only.
    /// </summary>
    public partial class CategoryItem : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _hasSelection;

        private bool _isLoaded;

        public CategoryItem(Category category, List<Element> elements, Predicate<ParameterItem> isParameterVisible)
        {
            Name = category.Name;
            Elements = elements;

            // Until the category is expanded, the placeholder gives the tree an arrow to expand it by.
            Parameters = [ParameterItem.CreatePlaceholder(this)];
            ParametersView = FilteredView.Create(Parameters, p => isParameterVisible((ParameterItem)p));
            SelectedParametersView = FilteredView.Create(Parameters, p => ((ParameterItem)p).IsSelected != false, nameof(ParameterItem.IsSelected));
        }

        public string Name { get; }
        public List<Element> Elements { get; }
        public ObservableCollection<ParameterItem> Parameters { get; }
        public ICollectionView ParametersView { get; }
        public ICollectionView SelectedParametersView { get; }

        partial void OnIsExpandedChanged(bool value)
        {
            if (value)
                LoadParameters();
        }

        private void LoadParameters()
        {
            if (_isLoaded) return;
            _isLoaded = true;

            var doc = Elements[0].Document;

            var instanceParameters = Elements
                .SelectMany(e => e.Parameters.Cast<Parameter>())
                .GroupBy(p => p.Definition.Name)
                .Select(g => new ParameterItem(this, g.First(), isTypeParameter: false));

            // Many elements share a type, so each type is read once.
            var typeParameters = Elements
                .Select(e => e.GetTypeId())
                .Distinct()
                .Select(id => id.ToElement(doc))
                .Where(t => t != null)
                .SelectMany(t => t.Parameters.Cast<Parameter>())
                .GroupBy(p => p.Definition.Name)
                .Select(g => new ParameterItem(this, g.First(), isTypeParameter: true));

            var parameters = instanceParameters
                .Concat(typeParameters)
                .OrderBy(p => p.Name, NaturalComparer.CurrentCultureIgnoreCase)
                .ThenByDescending(p => p.IsTypeParameter)
                .ToList();

            Parameters.Clear();
            Parameters.Add(ParameterItem.CreateAll(this));
            foreach (var parameter in parameters)
                Parameters.Add(parameter);
        }

        internal void UpdateSelection() => HasSelection = Parameters.Any(p => p.IsSelected != false);
    }

    /// <summary>
    /// A parameter of a category, or the "Все" item that takes the whole category. Its values load when it is
    /// expanded or ticked first. The tick is three-state: all values, some of them, none.
    /// </summary>
    public partial class ParameterItem : ObservableObject
    {
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool? _isSelected = false;

        private bool _isLoaded;

        // Set while the tick is passed between the parameter and its values, so that they do not answer each other.
        private bool _isSyncing;

        private ParameterItem(CategoryItem parent, string name, ElementId id, bool isTypeParameter, bool isAll, bool isPlaceholder)
        {
            Parent = parent;
            Name = name;
            Id = id;
            IsTypeParameter = isTypeParameter;
            IsAll = isAll;
            IsPlaceholder = isPlaceholder;

            Values = isAll || isPlaceholder ? [] : [new ValueItem(this, "…", [])];
            SelectedValuesView = FilteredView.Create(Values, v => ((ValueItem)v).IsSelected, nameof(ValueItem.IsSelected));
        }

        public ParameterItem(CategoryItem parent, Parameter parameter, bool isTypeParameter)
            : this(parent, parameter.Definition.Name, parameter.Id, isTypeParameter, isAll: false, isPlaceholder: false)
        {
        }

        public static ParameterItem CreateAll(CategoryItem parent) =>
            new(parent, "Все", ElementId.InvalidElementId, isTypeParameter: false, isAll: true, isPlaceholder: false);

        public static ParameterItem CreatePlaceholder(CategoryItem parent) =>
            new(parent, "…", ElementId.InvalidElementId, isTypeParameter: false, isAll: false, isPlaceholder: true);

        public CategoryItem Parent { get; }
        public string Name { get; }
        public ElementId Id { get; }
        public bool IsTypeParameter { get; }
        public bool IsAll { get; }
        public bool IsPlaceholder { get; }
        public ObservableCollection<ValueItem> Values { get; }
        public ICollectionView SelectedValuesView { get; }

        public IEnumerable<ElementId> SelectedElementIds => Values
            .Where(v => v.IsSelected)
            .SelectMany(v => v.ElementIds);

        partial void OnIsExpandedChanged(bool value)
        {
            if (value)
                LoadValues();
        }

        partial void OnIsSelectedChanged(bool? value)
        {
            if (_isSyncing) return;

            if (value is bool isSelected && !IsAll)
            {
                LoadValues();

                _isSyncing = true;
                foreach (var valueItem in Values)
                    valueItem.IsSelected = isSelected;
                _isSyncing = false;
            }

            Parent.UpdateSelection();
        }

        internal void OnValueSelectionChanged()
        {
            if (_isSyncing) return;

            _isSyncing = true;
            IsSelected = Values.All(v => v.IsSelected) ? true
                : Values.Any(v => v.IsSelected) ? null
                : false;
            _isSyncing = false;

            Parent.UpdateSelection();
        }

        private void LoadValues()
        {
            if (_isLoaded || IsAll || IsPlaceholder) return;
            _isLoaded = true;

            var doc = Parent.Elements[0].Document;
            var elementsByValue = new Dictionary<string, List<ElementId>>();
            var typeValues = new Dictionary<ElementId, string>();

            foreach (var element in Parent.Elements)
            {
                string value;
                if (IsTypeParameter)
                {
                    // Elements of one type share the value, so each type is read once.
                    var typeId = element.GetTypeId();
                    if (!typeValues.TryGetValue(typeId, out value))
                    {
                        value = ReadValue(typeId.ToElement(doc)?.ToParameter(Id));
                        typeValues[typeId] = value;
                    }
                }
                else
                {
                    value = ReadValue(element.ToParameter(Id));
                }

                if (value == null)
                    continue;

                if (!elementsByValue.TryGetValue(value, out var ids))
                {
                    ids = [];
                    elementsByValue[value] = ids;
                }
                ids.Add(element.Id);
            }

            Values.Clear();
            foreach (var kvp in elementsByValue
                .OrderBy(kvp => kvp.Key == NotSetValue ? 0 : 1)
                .ThenBy(kvp => kvp.Key == EmptyValue ? 0 : 1)
                .ThenBy(kvp => kvp.Key, NaturalComparer.CurrentCultureIgnoreCase))
            {
                Values.Add(new ValueItem(this, kvp.Key, kvp.Value));
            }
        }

        private const string NotSetValue = "[Не задан]";
        private const string EmptyValue = "[Нет значения]";

        /// <summary>The value as text; null when the element has no such parameter.</summary>
        private static string ReadValue(Parameter parameter)
        {
            if (parameter == null)
                return null;

            if (!parameter.HasValue)
                return NotSetValue;

            var value = parameter.StorageType == StorageType.String
                ? parameter.AsString()
                : parameter.AsValueString();

            return value.IsNullOrEmpty() ? EmptyValue : value;
        }
    }

    /// <summary>One value of a parameter and the elements that have it.</summary>
    public partial class ValueItem(ParameterItem parent, string value, List<ElementId> elementIds) : ObservableObject
    {
        [ObservableProperty] private bool _isSelected;

        public ParameterItem Parent { get; } = parent;
        public string Value { get; } = value;
        public List<ElementId> ElementIds { get; } = elementIds;

        partial void OnIsSelectedChanged(bool value) => Parent.OnValueSelectionChanged();
    }

    internal static class FilteredView
    {
        /// <summary>
        /// A view of the list with a filter of its own. With <paramref name="liveProperty"/>, an item that changes this
        /// property comes into the view or leaves it at once, without refreshing the view.
        /// </summary>
        public static ICollectionView Create(IList source, Predicate<object> filter, string liveProperty = null)
        {
            var view = new ListCollectionView(source) { Filter = filter };

            if (liveProperty != null)
            {
                view.IsLiveFiltering = true;
                view.LiveFilteringProperties.Add(liveProperty);
            }

            return view;
        }
    }
}
