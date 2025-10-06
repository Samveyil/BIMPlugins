using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage.Methods;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Levels.WPF
{
    public partial class MoveLevelViewModel : ObservableObject
    {
        [ObservableProperty] private Level _selectedLevel;
        [ObservableProperty] private double _elevation;

        partial void OnSelectedLevelChanged(Level value)
        {
            Elevation = UnitUtils.ConvertFromInternalUnits(value.Elevation, ParameterMethods.GetUnitType()).Round(3);
        }

        private ExternalEvent ExEvent { get; set; }

        public List<Level> Levels { get; } = RevitAPI.Document.ToElements<Level>().OrderBy(l => l.Elevation).ToList();

        public MoveLevelViewModel()
        {
            var handler = new RevitAPI.MyEventHandler<MoveLevelViewModel>(
                this,
                vm => vm.MoveLevel()
            );
            ExEvent = ExternalEvent.Create(handler);
        }

        [RelayCommand]
        private void Run() => ExEvent.Raise();
        private void MoveLevel()
        {
            RevitOptionsBar.Hide();

            var levelFilter = new ElementLevelFilter(SelectedLevel.Id);

            var scheduleLevelRule = new ElementId(BuiltInParameter.INSTANCE_SCHEDULE_ONLY_LEVEL_PARAM).CreateEqualsFilter(SelectedLevel.Id);
            var levelRule = new ElementId(BuiltInParameter.FAMILY_LEVEL_PARAM).CreateEqualsFilter(SelectedLevel.Id);

            var bottomFiltersList = new List<ElementFilter> { scheduleLevelRule, levelRule, levelFilter };
            var topFiltersList = new List<ElementFilter> { new ElementId(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM).CreateEqualsFilter(SelectedLevel.Id) };
            foreach (var bip in Enum.GetValues(typeof(BuiltInParameter)).Cast<BuiltInParameter>())
            {
                try
                {
                    if (LabelUtils.GetLabelFor(bip) == "Базовый уровень")
                        bottomFiltersList.Add(new ElementId(bip).CreateEqualsFilter(SelectedLevel.Id));
                    else if (LabelUtils.GetLabelFor(bip) == "Зависимость сверху")
                        topFiltersList.Add(new ElementId(bip).CreateEqualsFilter(SelectedLevel.Id));
                }
                catch { }
            }

            var bottomLevelsFilter = new LogicalOrFilter(bottomFiltersList);
            var topLevelsFilter = new LogicalOrFilter(topFiltersList);
            
            var bottomElements = RevitAPI.Document.ToModelElements(bottomLevelsFilter).ToList();
            var topElements = RevitAPI.Document.ToModelElements(topLevelsFilter).ToList();

            var projectElevationOffset = SelectedLevel.Elevation - SelectedLevel.ProjectElevation;
            var intElevation = UnitUtils.ConvertToInternalUnits(Elevation, ParameterMethods.GetUnitType());

            var elementsWithNewLevel = new List<Element>();

            using (Transaction t = new Transaction(RevitAPI.Document, "Переместить уровень"))
            {
                t.Start();

                var newLevel = Level.Create(RevitAPI.Document, intElevation - projectElevationOffset);
                var levelOffset = newLevel.ProjectElevation - SelectedLevel.ProjectElevation;

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run($"Перемещение уровня...", bottomElements, (element) =>
                    {
                        if (element.LevelId != ElementId.InvalidElementId || element.get_Parameter(BuiltInParameter.STAIRS_BASE_LEVEL_PARAM) != null)
                        {
                            var offsetParameter = GetBottomOffsetParameter(element);
                            if (offsetParameter == null) return;

                            var offset = offsetParameter.AsDouble() - levelOffset;

                            if (!offsetParameter.IsReadOnly)
                                offsetParameter.Set(offset);
                        }
                        else
                        {
                            var levelParameter = element.Parameters.Cast<Parameter>()
                                .Where(p => !p.IsReadOnly && ((InternalDefinition)p.Definition).BuiltInParameter != BuiltInParameter.INVALID)
                                .OrderBy(p => p.Definition.Name)
                                .FirstOrDefault(p => p.Definition.Name.ToLower().Contains("уровень"));

                            if (levelParameter == null) return;

                            levelParameter.Set(newLevel.Id);
                            elementsWithNewLevel.Add(element);
                        }
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                SelectedLevel.Elevation = newLevel.Elevation;
                foreach (var element in elementsWithNewLevel)
                {
                    var levelParameter = element.Parameters.Cast<Parameter>()
                        .Where(p => !p.IsReadOnly && ((InternalDefinition)p.Definition).BuiltInParameter != BuiltInParameter.INVALID)
                        .OrderBy(p => p.Definition.Name)
                        .FirstOrDefault(p => p.Definition.Name.ToLower().Contains("уровень"));
                    
                    levelParameter.Set(SelectedLevel.Id);
                }

                RevitAPI.Document.Delete(newLevel.Id);

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run($"Перемещение уровня...", topElements, (element) =>
                    {
                        if (element.LevelId != ElementId.InvalidElementId || element.get_Parameter(BuiltInParameter.STAIRS_TOP_LEVEL_PARAM) != null)
                        {
                            var offsetParameter = element.ToParameter("Смещение сверху");
                            if (offsetParameter == null) return;

                            var offset = offsetParameter.AsDouble() - levelOffset;

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
        private Parameter GetBottomOffsetParameter(Element element)
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
    }
}