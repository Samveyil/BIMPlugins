using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Levels.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Levels.WPF
{
    public partial class SetNewLevelVM : ObservableObject
    {
        private const string Title = "Назначить уровень";

        [ObservableProperty] private int _count;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(SelectAllCommand))] private Level _oldLevel;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Level _newLevel;

        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly ExternalEvent _exEvent;

        // The elements to put on the new level. Groups and their members are left out: they are told when it is done.
        private List<Element> _elements = [];
        private List<Element> _groupElements = [];

        public List<Level> Levels { get; }

        public SetNewLevelVM()
        {
            _uiDoc = RevitAPI.UIDocument;
            _doc = _uiDoc.Document;
            _exEvent = RevitAPI.CreateExtEvent(this, vm => vm.SetNewLevel());

            Levels = _doc.ToElements<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        [RelayCommand]
        private void SelectElems()
        {
            RevitOptionsBar.Hide(true);
            try
            {
                var selectedElements = _uiDoc.ToSelectedElements();
                var elements = selectedElements.Count != 0
                    ? selectedElements
                    : _uiDoc.PickElements("Выберите элементы");

                if (elements != null)
                    SetElements(elements);
            }
            finally
            {
                RevitOptionsBar.Show();
            }
        }

        [RelayCommand(CanExecute = nameof(CanSelectAll))]
        private void SelectAll() => SetElements(LevelBindings.FindElements(_doc, OldLevel.Id, LevelBindings.Bottom, withOwnLevel: true));

        private bool CanSelectAll() => OldLevel != null;

        // A new choice replaces the one before.
        private void SetElements(IEnumerable<Element> elements)
        {
            var isInGroup = elements.ToLookup(e => e is Group || e.GroupId != ElementId.InvalidElementId);
            _elements = isInGroup[false].ToList();
            _groupElements = isInGroup[true].ToList();

            Count = _elements.Count;
            RunCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run() => _exEvent.Raise();

        private bool CanRun() => NewLevel != null && _elements.Count != 0;

        private void SetNewLevel()
        {
            RevitOptionsBar.Hide();

            var notChanged = _groupElements
                .Select(e => LevelBindings.Describe(e) + ": в группе")
                .ToList();

            using (Transaction t = new Transaction(_doc, Title))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Назначение нового уровня...", _elements, element =>
                    {
                        if (!SetLevel(element))
                            notChanged.Add(LevelBindings.Describe(element) + ": нет параметра уровня, который можно изменить");
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                t.Commit();
            }

            if (notChanged.Count != 0)
                ReportDialog.Show(notChanged, "Уровень не назначен");
        }

        // The element goes to the new level and keeps its place: its offset changes by the height between the levels.
        private bool SetLevel(Element element)
        {
            var binding = LevelBindings.Bottom.FirstOrDefault(b => element.get_Parameter(b.Level) is { IsReadOnly: false });
            if (binding == null)
                return false;

            var levelParameter = element.get_Parameter(binding.Level);
            var oldLevel = levelParameter.AsElementId().ToElement<Level>(_doc);
            if (oldLevel != null && oldLevel.Id.Equals(NewLevel.Id))
                return true;

            // An element that keeps its place by itself needs only the new level.
            var offset = binding.KeepsOffset(element) ? binding.GetOffset(element) : null;
            var offsetValue = offset?.AsDouble();

            levelParameter.Set(NewLevel.Id);

            if (offset != null && oldLevel != null)
                offset.Set(offsetValue.Value - (NewLevel.ProjectElevation - oldLevel.ProjectElevation));

            return true;
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}
