using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Levels.WPF
{
    public partial class SetNewLevelViewModel : ObservableObject
    {
        [ObservableProperty] private double _count = 0;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SelectAllCommand))] private Level _oldLevel;
        [ObservableProperty] private Level _newLevel;

        private List<Element> _elements = [];

        private ExternalEvent ExEvent { get; set; }

        public List<Level> Levels { get; } = RevitAPI.Document.ToElements<Level>().OrderBy(l => l.Elevation).ToList();

        public SetNewLevelViewModel()
        {
            var handler = new RevitAPI.MyEventHandler<SetNewLevelViewModel>(
                this,
                vm => vm.SetNewLevel()
            );
            ExEvent = ExternalEvent.Create(handler);
        }


        [RelayCommand]
        private void SelectElems()
        {
            RevitOptionsBar.Hide(true);
            try
            {
                var selectedElems = RevitAPI.UIDocument.ToSelectedElements().ToList();

                _elements = selectedElems.Count != 0
                    ? selectedElems
                    : RevitAPI.UIDocument.PickObjects("Выберите элементы").ToList();
                
                Count = _elements.Count;
            }
            catch { }
            finally
            {
                RevitOptionsBar.Show();
            }
        }
        
        [RelayCommand(CanExecute = nameof(IsSelectEnabled))]
        private void SelectAll()
        {
            var levelFilter = new ElementLevelFilter(OldLevel.Id);

            var scheduleLevelRule = new ElementId(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM).CreateEqualsFilter(OldLevel.Id);
            var levelRule = new ElementId(BuiltInParameter.FAMILY_LEVEL_PARAM).CreateEqualsFilter(OldLevel.Id);

            var filtersList = new List<ElementFilter> { scheduleLevelRule, levelRule, levelFilter };
            foreach (var baseLevelBIP in Enum.GetValues(typeof(BuiltInParameter)).Cast<BuiltInParameter>())
            {
                try
                {
                    if (LabelUtils.GetLabelFor(baseLevelBIP) == "Базовый уровень")
                        filtersList.Add(new ElementId(baseLevelBIP).CreateEqualsFilter(OldLevel.Id));
                }
                catch { }
            }

            var levelsFilter = new LogicalOrFilter(filtersList);

            _elements = RevitAPI.Document.ToModelElements(levelsFilter).ToList();
            Count = _elements.Count;
        }

        [RelayCommand]
        private void Run() => ExEvent.Raise();
        private void SetNewLevel()
        {
            RevitOptionsBar.Hide();

            using (Transaction t = new Transaction(RevitAPI.Document, "Назначить уровень"))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run($"Назначение нового уровня...", _elements, (element) =>
                    {
                        Parameter levelParameter = null;
                        foreach (var param in element.Parameters.Cast<Parameter>().Where(p => !p.IsReadOnly && ((InternalDefinition)p.Definition).BuiltInParameter != BuiltInParameter.INVALID).OrderBy(p => p.Definition.Name))
                        {
                            if (param.Definition.Name.ToLower().Contains("уровень"))
                            {
                                levelParameter = param;
                                break;
                            }
                            else if (param.Definition.Name.Contains("Зависимость снизу"))
                            {
                                levelParameter = param;
                                break;
                            }
                        }

                        if (levelParameter == null) return;

                        if (element.LevelId == ElementId.InvalidElementId && ((InternalDefinition)levelParameter.Definition).BuiltInParameter != BuiltInParameter.STAIRS_BASE_LEVEL_PARAM)
                        {
                            levelParameter.Set(NewLevel.Id);
                        }
                        else
                        {
                            var offsetParameter = GetOffsetParameter(element);
                            if (offsetParameter == null) return;

                            var levelOffset = NewLevel.ProjectElevation - levelParameter.AsElementId().ToElement<Level>().ProjectElevation;
                            var offset = offsetParameter.AsDouble() - levelOffset;

                            levelParameter.Set(NewLevel.Id);
                            if (!offsetParameter.IsReadOnly)
                                offsetParameter.Set(offset);
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
        private Parameter GetOffsetParameter(Element element)
        {
            string[] parameterNames = ["Смещение снизу", "Отметка от уровня", "Смещение от уровня", "Смещение от главной модели", "Высота нижнего бруса"];

            foreach (var paramName in parameterNames)
            {
                var parameter = element.LookupParameter(paramName);
                if (parameter != null)
                {
                    return parameter;
                }
            }

            return null;
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();

        private bool IsSelectEnabled() => OldLevel != null;
    }
}