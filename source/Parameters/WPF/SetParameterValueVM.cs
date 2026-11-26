using Autodesk.Revit.DB;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace BIMPlugins.Parameters.WPF
{
    // Where the value written to the parameter comes from.
    public enum ValueSource { UserValue, Parameter, Formula }

    public partial class SetParameterValueVM : ObservableObject
    {
        private const string Title = "Заполнить параметры";

        // A parameter in the formula: "[Ширина]".
        private static readonly Regex FormulaParameter = new(@"\[(.*?)\]");

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UserValue), nameof(FromParameter), nameof(UserFormula))]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private ValueSource _source = ValueSource.UserValue;

        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _sourceParameter;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _targetParameter;
        [ObservableProperty] private string _userValueText;
        [ObservableProperty] private string _userFormulaText;

        private readonly ICollection<Element> _elements;

        public List<Parameter> Parameters { get; }
        public ICollectionView TargetParameters { get; }

        // The radio buttons of the three sources.
        public bool UserValue
        {
            get => Source == ValueSource.UserValue;
            set { if (value) Source = ValueSource.UserValue; }
        }

        public bool FromParameter
        {
            get => Source == ValueSource.Parameter;
            set { if (value) Source = ValueSource.Parameter; }
        }

        public bool UserFormula
        {
            get => Source == ValueSource.Formula;
            set { if (value) Source = ValueSource.Formula; }
        }

        partial void OnSourceChanged(ValueSource value) => TargetParameters.Refresh();
        partial void OnSourceParameterChanged(Parameter value) => TargetParameters.Refresh();

        public SetParameterValueVM(ICollection<Element> elements)
        {
            _elements = elements;

            // Parameters every selected element has, and those every type of them has. Some elements, as rooms, have no type.
            var instanceParameters = CommonParameters(elements);
            var types = elements
                .Select(e => e.ToElementType())
                .Where(t => t != null)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            Parameters = instanceParameters
                .Concat(CommonParameters(types))
                .OrderBy(p => p.Definition.Name)
                .ToList();

            TargetParameters = CollectionViewSource.GetDefaultView(Parameters.ToList());
            TargetParameters.Filter = item => CanWrite((Parameter)item);
        }

        private static IEnumerable<Parameter> CommonParameters<T>(ICollection<T> elements) where T : Element => elements
            .SelectMany(e => e.GetOrderedParameters().Where(p => p.StorageType != StorageType.None))
            .GroupBy(p => p.Id)
            .Where(g => g.Count() == elements.Count)
            .Select(g => g.First());

        // A target parameter the chosen source can be written to.
        private bool CanWrite(Parameter target) => Source switch
        {
            ValueSource.Formula => target.StorageType == StorageType.String,
            ValueSource.Parameter when SourceParameter?.StorageType == StorageType.ElementId =>
                target.StorageType is StorageType.String or StorageType.ElementId,
            _ => target.StorageType != StorageType.ElementId
        };

        [RelayCommand]
        private void AddParameterToFormula()
        {
            var vm = new AddParameterToFormulaVM(Parameters);
            new AddParameterToFormulaWindow(vm).ShowDialog();

            if (vm.UserSelect)
                UserFormulaText += $"[{vm.SelectedParameter.Definition.Name}]";
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            RaiseCloseRequest();

            // Elements whose parameter could not take the value are told at the end.
            var failed = new List<string>();

            using (var t = new Transaction(_elements.First().Document, Title))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Обработка элементов...", _elements, element =>
                    {
                        var target = FindParameter(element, TargetParameter.Id);
                        if (target == null || target.IsReadOnly)
                            return;

                        bool isSet;
                        try
                        {
                            isSet = SetValue(element, target);
                        }
                        catch (Autodesk.Revit.Exceptions.ApplicationException)
                        {
                            isSet = false;
                        }

                        if (!isSet)
                            failed.Add($"{element.Name} (Id {element.Id})");
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                t.Commit();
            }

            if (failed.Count != 0)
                ReportDialog.Show(failed, $"Не удалось записать «{TargetParameter.Definition.Name}»");
        }

        private bool CanRun() => TargetParameter != null && (Source != ValueSource.Parameter || SourceParameter != null);

        private bool SetValue(Element element, Parameter target)
        {
            switch (Source)
            {
                case ValueSource.UserValue:
                    return SetText(target, UserValueText);

                case ValueSource.Parameter:
                    var source = FindParameter(element, SourceParameter.Id);
                    if (source == null)
                        return false;
                    if (!source.HasValue && !target.HasValue)
                        return true;

                    return CopyValue(source, target);

                default:
                    return target.Set(ParseFormula(element, UserFormulaText ?? string.Empty));
            }
        }

        // The parameter of the element or, failing that, of its type.
        private static Parameter FindParameter(Element element, ElementId parameterId) =>
            element.ToParameter(parameterId) ?? element.ToElementType()?.ToParameter(parameterId);

        private static Parameter FindParameter(Element element, string parameterName) =>
            element.ToParameter(parameterName) ?? element.ToElementType()?.ToParameter(parameterName);

        private static bool CopyValue(Parameter source, Parameter target)
        {
            if (source.StorageType == target.StorageType)
                return target.SetValue(source.GetValue());

            // Between other kinds the value goes as the text the user sees: "1500" of a length in millimetres.
            var text = source.StorageType == StorageType.String ? source.AsString() : source.AsValueString();
            return SetText(target, text);
        }

        // Text into a parameter of any kind; a number is read in the project units, with a dot or a comma.
        private static bool SetText(Parameter target, string text)
        {
            text ??= string.Empty;

            switch (target.StorageType)
            {
                case StorageType.String:
                    return target.Set(text);

                case StorageType.Integer when target.SetValue(text):
                    return true;

                case StorageType.Integer:
                case StorageType.Double:
                    return TrySetValueString(target, text)
                        || TrySetValueString(target, text.Contains(',') ? text.Replace(',', '.') : text.Replace('.', ','));

                default:
                    return false;
            }
        }

        // Revit reads the text as the project units show numbers; text it cannot read is false, not an exception.
        private static bool TrySetValueString(Parameter target, string text)
        {
            try
            {
                return target.SetValueString(text);
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return false;
            }
        }

        // The formula with each "[Name]" replaced by the value of that parameter; an unknown name stays as it is.
        private static string ParseFormula(Element element, string formula) => FormulaParameter.Replace(formula, match =>
        {
            var parameter = FindParameter(element, match.Groups[1].Value);
            if (parameter == null)
                return match.Value;

            return (parameter.StorageType == StorageType.String ? parameter.AsString() : parameter.AsValueString()) ?? string.Empty;
        });

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
