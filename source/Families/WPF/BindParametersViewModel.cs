using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace BIMPlugins.Families.WPF
{
    public partial class BindParametersViewModel : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<BindParameterItem> _parameters = [];

        public BindParametersViewModel(FamilyInstance familyInstance)
        {
            var famManager = RevitAPI.Document.FamilyManager;
            var famParameters = famManager.Parameters.Cast<FamilyParameter>().OrderBy(p => p.Definition.Name).ToList();

            foreach (Parameter parameter in familyInstance.GetOrderedParameters().Cast<Parameter>().Where(p => famManager.CanElementParameterBeAssociated(p)))
            {
                if (famManager.CanElementParameterBeAssociated(parameter))
                {
                    var filteredParameters = famParameters.Where(p => p.GetParameterType() == parameter.GetParameterType()).ToList();

                    var bindParameterItem = new BindParameterItem(parameter, filteredParameters)
                    {
                        BindParameter = famManager.GetAssociatedFamilyParameter(parameter)
                    };

                    Parameters.Add(bindParameterItem);
                }
            }

            foreach (Parameter parameter in familyInstance.Symbol.GetOrderedParameters().Cast<Parameter>().Where(p => famManager.CanElementParameterBeAssociated(p)))
            {
                if (famManager.CanElementParameterBeAssociated(parameter))
                {
                    var filteredParameters = famParameters.Where(p => p.GetParameterType() == parameter.GetParameterType()).ToList();

                    var bindParameterItem = new BindParameterItem(parameter, filteredParameters, true)
                    {
                        BindParameter = famManager.GetAssociatedFamilyParameter(parameter)
                    };

                    Parameters.Add(bindParameterItem);
                }
            }

            Parameters = new(Parameters.OrderBy(p => p.ParameterGroup).ToList());
        }

        [RelayCommand]
        private void BindByName()
        {
            foreach (var bindParamItem in Parameters.Where(p => p.BindParameter == null))
            {
                bindParamItem.BindParameter = bindParamItem.FamilyParameters.FirstOrDefault(p => p.Definition.Name == bindParamItem.Parameter.Definition.Name);
            }
        }

        [RelayCommand]
        private void ClearBindings()
        {
            foreach (var bindParamItem in Parameters)
            {
                bindParamItem.BindParameter = null;
            }
        }

        [RelayCommand]
        private void Run()
        {
            var famManager = RevitAPI.Document.FamilyManager;

            using (Transaction t = new Transaction(RevitAPI.Document, "Связать параметры"))
            {
                t.Start();

                foreach (var bindParamItem in Parameters)
                {
                    famManager.AssociateElementParameterToFamilyParameter(bindParamItem.Parameter, bindParamItem.BindParameter);
                }

                t.Commit();
            }

            RaiseCloseRequest();
        }


        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }

    public partial class BindParameterItem(Parameter parameter, List<FamilyParameter> familyParameters, bool isType=false) : ObservableObject
    {
        [ObservableProperty] private FamilyParameter _bindParameter;

        public bool ParameterType { get; } = isType;
        public Parameter Parameter { get; } = parameter;
        public string ParameterGroup { get; } = LabelUtils.GetLabelFor(parameter.Definition.ParameterGroup);
        public List<FamilyParameter> FamilyParameters { get; } = familyParameters;
    }
}