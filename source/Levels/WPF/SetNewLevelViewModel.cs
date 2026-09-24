using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Levels.WPF
{
    public partial class SetNewLevelViewModel : ObservableObject
    {
        [ObservableProperty] private double _count = 0;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SelectAllCommand))] private Level _oldLevel;
        [ObservableProperty] private Level _newLevel;

        private readonly Document _doc;
        private readonly List<Element> _elements = [];

        private ExternalEvent ExEvent { get; }

        public List<Level> Levels { get; }

        public SetNewLevelViewModel()
        {
            _doc = RevitAPI.Document;
            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.SetNewLevel());

            Levels = _doc.ToElements<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }


        [RelayCommand]
        private void SelectElems()
        {
            var uiDoc = new UIDocument(_doc);

            RevitOptionsBar.Hide(true);
            try
            {
                var selectedElems = uiDoc.ToSelectedElements().ToList();

                var elems = selectedElems.Count != 0
                    ? selectedElems
                    : uiDoc.PickElements("Выберите элементы").ToList();
                
                Count = elems.Count;

                var _uniqueIds = new HashSet<ElementId>();
                foreach (var elem in elems)
                {
                    if (elem.GroupId != ElementId.InvalidElementId)
                    {
                        if (_uniqueIds.Add(elem.GroupId))
                            _elements.Add(elem.GroupId.ToElement(_doc));
                    }
                    else if (_uniqueIds.Add(elem.Id))
                        _elements.Add(elem);
                }
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

            var elems = _doc.ToModelElements(levelsFilter).ToList();
            Count = elems.Count;

            var _uniqueIds = new HashSet<ElementId>();
            foreach (var elem in elems)
            {
                if (elem.GroupId != ElementId.InvalidElementId)
                {
                    if (_uniqueIds.Add(elem.GroupId))
                        _elements.Add(elem.GroupId.ToElement(_doc));
                }
                else if (_uniqueIds.Add(elem.Id))
                    _elements.Add(elem);
            }
        }

        [RelayCommand]
        private void Run() => ExEvent.Raise();
        private void SetNewLevel()
        {
            RevitOptionsBar.Hide();

            using (Transaction t = new Transaction(_doc, "Назначить уровень"))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run($"Назначение нового уровня...", _elements, (element) =>
                    {
                        if (element is Group group)
                        {
                            var groupType = group.GroupType;
                            var groupName = groupType.Name;

                            var memberIds = group.UngroupMembers();
                            foreach (var memberId in memberIds)
                                SetLevel(memberId.ToElement(_doc));

                            _doc.Regenerate();

                            var grpNew = _doc.Create.NewGroup(memberIds);
                            var grpTypeNew = grpNew.GroupType;

                            //foreach (var oldGroup in RevitAPI.Document.ToElements<Group>().Where(g => g.GroupType.Name == groupName))
                            //    oldGroup.GroupType = grpTypeNew;

                            //RevitAPI.Document.Delete(groupType.Id);

                            grpTypeNew.Name = $"{groupName} - {grpTypeNew.Name}";

                            //RevitAPI.Document.Regenerate();

                            //grpNew.GroupType = groupId;
                        }
                        else
                            SetLevel(element);
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
        private void SetLevel(Element element)
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
                else if (param.Definition.Name.Contains("Базовая зависимость"))
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

                var levelOffset = NewLevel.ProjectElevation - levelParameter.AsElementId().ToElement<Level>(_doc).ProjectElevation;
                var offset = offsetParameter.AsDouble() - levelOffset;

                levelParameter.Set(NewLevel.Id);
                if (!offsetParameter.IsReadOnly)
                    offsetParameter.Set(offset);
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