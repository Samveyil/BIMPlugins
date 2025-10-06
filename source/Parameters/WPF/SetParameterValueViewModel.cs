using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows.Data;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Parameters.WPF
{
    public partial class SetParameterValueViewModel : ObservableObject
    {
        [ObservableProperty] private bool _userValue = false;
        [ObservableProperty] private bool _fromParameter = false;
        [ObservableProperty] private bool _userFormula = false;
        [ObservableProperty] private Parameter _sourceParameter;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Parameter _targetParameter;
        [ObservableProperty] private string _userValueText;
        [ObservableProperty] private string _userFormulaText;

        private readonly ICollection<Element> _elements;
        private bool _elementIdParamSelected;

        public List<Parameter> Parameters { get; } = [];
        public ICollectionView TargetParameters { get; }

        partial void OnUserFormulaChanged(bool value)
        {
            TargetParameters.Refresh();
        }
        partial void OnSourceParameterChanged(Parameter value)
        {
            _elementIdParamSelected = value.StorageType == StorageType.ElementId;
            TargetParameters.Refresh();
        }

        public SetParameterValueViewModel(ICollection<Element> elements)
        {
            _elements = elements;

            var instParameters = elements.SelectMany(e => e.GetOrderedParameters()
                .Where(p => p.StorageType != StorageType.None))
                .GroupBy(p => p.Id.ToString())
                .Select(g => new { Id = g.Key, Count = g.Count(), Parameter = g.First() })
                .Where(x => x.Count == elements.Count)
                .Select(x => x.Parameter);

            var typeElements = elements
                .Select(e => e.ToElementType())
                .GroupBy(t => t.Id.ToString())
                .Select(g => g.First())
                .ToList();

            var typeParameters = typeElements.SelectMany(t => t.GetOrderedParameters())
                .Where(p => p.StorageType != StorageType.None)
                .GroupBy(p => p.Id.ToString())
                .Select(g => new { Id = g.Key, Count = g.Count(), Parameter = g.First() })
                .Where(x => x.Count == typeElements.Count)
                .Select(x => x.Parameter);

            Parameters = instParameters.Concat(typeParameters)
                .OrderBy(p => p.Definition.Name)
                .ToList();

            TargetParameters = CollectionViewSource.GetDefaultView(Parameters.ToList());
            TargetParameters.Filter = item =>
            {
                var parameter = (Parameter)item;
                
                var elementIdFilter = (!_elementIdParamSelected && parameter.StorageType != StorageType.ElementId) ||
                                      (_elementIdParamSelected && (parameter.StorageType == StorageType.String || parameter.StorageType == StorageType.ElementId));
                var formulaFilter = !UserFormula || parameter.StorageType == StorageType.String;

                return elementIdFilter && formulaFilter;
            };
        }

        [RelayCommand]
        private void AddParameterToFormula()
        {
            var viewModel = new AddParameterToFormulaViewModel(Parameters);
            var window = new AddParameterToFormulaWindow(viewModel);
            viewModel.CloseRequest += (s, e) => window.Close();

            window.ShowDialog();

            if (viewModel.UserSelect)
                UserFormulaText += ($"[{viewModel.SelectedParameter.Definition.Name}]");
        }

        [RelayCommand(CanExecute = nameof(IsEnabled))]
        private void Run()
        {
            RaiseCloseRequest();

            if (UserValue)
            {
                using (Transaction t = new Transaction(RevitAPI.Document, "Заполнить параметры"))
                {
                    t.Start();

                    using (var revitProgressBar = new RevitProgressBar(true))
                    {
                        revitProgressBar.Run("Обработка элементов...", _elements, (element) =>
                        {
                            var parameter = element.ToParameter(TargetParameter.Id) ?? element.ToElementType().ToParameter(TargetParameter.Id);
                            if (parameter == null || parameter.IsReadOnly) return;

                            if (parameter.StorageType == StorageType.Integer || parameter.StorageType == StorageType.Double)
                            {
                                var value = Convert.ToDouble(UserValueText.Replace(",", "."));

                                parameter.Set(parameter.StorageType == StorageType.Double
                                    ? UnitUtils.ConvertToInternalUnits(value, parameter.GetUnitType())
                                    : value);
                            }
                            else
                                parameter.Set(UserValueText);
                        });

                        if (revitProgressBar.IsCancelling())
                        {
                            t.RollBack();
                            return;
                        }
                    }

                    t.Commit();
                }
            }
            else if (FromParameter)
            {
                using (Transaction t = new Transaction(RevitAPI.Document, "Заполнить параметры"))
                {
                    t.Start();

                    using (var revitProgressBar = new RevitProgressBar(true))
                    {
                        revitProgressBar.Run("Обработка элементов...", _elements, (element) =>
                        {
                            var sourceParameter = element.ToParameter(SourceParameter.Id) ?? element.ToElementType().ToParameter(SourceParameter.Id);
                            if (sourceParameter == null) return;

                            var targetParameter = element.ToParameter(TargetParameter.Id) ?? element.ToElementType().ToParameter(TargetParameter.Id);
                            if (targetParameter == null || targetParameter.IsReadOnly) return;

                            if (!sourceParameter.HasValue && !targetParameter.HasValue) return;

                            if (sourceParameter.StorageType == targetParameter.StorageType)
                                targetParameter.SetValue(sourceParameter.GetValue());
                            else if (sourceParameter.StorageType == StorageType.String || targetParameter.StorageType == StorageType.String)
                            {
                                if (sourceParameter.StorageType == StorageType.String)
                                {
                                    var value = Convert.ToDouble(sourceParameter.AsString().Replace(",", "."));
                                    targetParameter.Set(targetParameter.StorageType == StorageType.Double
                                        ? UnitUtils.ConvertToInternalUnits(value, targetParameter.GetUnitType())
                                        : value.Round(0)
                                    );
                                }
                                else
                                    targetParameter.Set(sourceParameter.AsValueString());
                            }
                            else
                            {
                                targetParameter.Set(targetParameter.StorageType == StorageType.Integer ? sourceParameter.AsDouble().Round(0) : sourceParameter.AsInteger());
                            }
                        });

                        if (revitProgressBar.IsCancelling())
                        {
                            t.RollBack();
                            return;
                        }
                    }

                    t.Commit();
                }
            }
            else
            {
                using (Transaction t = new Transaction(RevitAPI.Document, "Заполнить параметры"))
                {
                    t.Start();

                    using (var revitProgressBar = new RevitProgressBar(true))
                    {
                        revitProgressBar.Run("Обработка элементов...", _elements, (element) =>
                        {
                            var targetParameter = element.ToParameter(TargetParameter.Id) ?? element.ToElementType().ToParameter(TargetParameter.Id);
                            if (targetParameter == null || targetParameter.IsReadOnly) return;

                            targetParameter.Set(ParseParameterFormula(element, UserFormulaText));
                        });

                        if (revitProgressBar.IsCancelling())
                        {
                            t.RollBack();
                            return;
                        }
                    }

                    t.Commit();
                }
            }
        }

        private string ParseParameterFormula(Element element, string template)
        {
            string pattern = @"\[(.*?)\]";

            return Regex.Replace(template, pattern, match =>
            {
                string paramName = match.Groups[1].Value;
                Parameter param = element.ToParameter(paramName) ?? element.ToElementType().ToParameter(paramName);

                if (param != null)
                {
                    return param.StorageType == StorageType.String ? param.AsString() : param.AsValueString();
                }

                return match.Value;
            });
        }
        private bool IsEnabled() => TargetParameter != null;

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}