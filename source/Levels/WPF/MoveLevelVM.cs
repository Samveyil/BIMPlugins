using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Levels.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Levels.WPF
{
    public partial class MoveLevelVM : ObservableObject
    {
        private const string Title = "Переместить уровень";

        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Level _selectedLevel;
        [ObservableProperty] private double _elevation;

        partial void OnSelectedLevelChanged(Level value)
        {
            if (value != null)
                Elevation = value.Elevation.ToMillimeters().Round(3);
        }

        private readonly Document _doc;
        private readonly ExternalEvent _exEvent;

        public List<Level> Levels { get; }

        public MoveLevelVM()
        {
            _doc = RevitAPI.Document;
            _exEvent = RevitAPI.CreateExtEvent(this, vm => vm.MoveLevel());

            Levels = _doc.ToElements<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run() => _exEvent.Raise();

        private bool CanRun() => SelectedLevel != null;

        // The level goes to the new elevation; the elements on it and under it keep their place: their offsets
        // from the level change by as much as the level moves.
        private void MoveLevel()
        {
            RevitOptionsBar.Hide();

            var level = SelectedLevel;
            var newElevation = Elevation.FromMillimeters();
            var shift = newElevation - level.Elevation;
            if (Math.Abs(shift) < 1e-9)
                return;

            // A group follows its members bound to the level, whatever its own offset: so the members keep their place
            // through their offsets, as other elements do, and the group itself is left as it is.
            // Instances on the work plane of the level are kept by their offset from it, whatever level parameters they have.
            var hosted = LevelBindings.FindHosted(_doc, level.Id, withGroupMembers: true);
            var hostedIds = hosted.Select(fi => fi.Id).ToHashSet();

            var bottomElements = LevelBindings.FindElements(_doc, level.Id, LevelBindings.Bottom, withOwnLevel: true, withGroupMembers: true)
                .Where(e => e is not Group && !hostedIds.Contains(e.Id))
                .Concat(hosted)
                .ToList();
            var topElements = LevelBindings.FindElements(_doc, level.Id, LevelBindings.Top, withOwnLevel: false, withGroupMembers: true);

            // Elements that keep their place only by their level: they stand on a copy of the level at the new elevation
            // while the level moves, and then go back to it.
            var reattached = new List<Parameter>();
            Level levelCopy = null;

            // Elements whose place could not be kept: they move with the level.
            var moved = new List<string>();

            using (Transaction t = new Transaction(_doc, Title))
            {
                t.Start();

                using (var revitProgressBar = new RevitProgressBar(true))
                {
                    revitProgressBar.Run("Перемещение уровня...", bottomElements, element =>
                    {
                        if (hostedIds.Contains(element.Id))
                        {
                            var hostOffsets = LevelBindings.GetHostOffsets(element);
                            // Both ends of a beam are shifted even if one fails.
                            var shifted = hostOffsets.Select(p => LevelBindings.Shift(p, shift)).ToList();
                            if (shifted.Count == 0 || shifted.Contains(false))
                                moved.Add(LevelBindings.Describe(element));

                            return;
                        }

                        var binding = LevelBindings.Find(element, LevelBindings.Bottom, level.Id);
                        if (binding == null)
                        {
                            moved.Add(LevelBindings.Describe(element));
                            return;
                        }

                        if (binding.KeepsOffset(element))
                        {
                            var offset = binding.GetOffset(element);
                            if (offset == null || !LevelBindings.Shift(offset, shift))
                                moved.Add(LevelBindings.Describe(element));

                            return;
                        }

                        var levelParameter = element.get_Parameter(binding.Level);
                        if (!levelParameter.IsReadOnly)
                        {
                            levelCopy ??= level.Copy(new XYZ(0, 0, shift)).First().ToElement<Level>(_doc);
                            levelParameter.Set(levelCopy.Id);
                            reattached.Add(levelParameter);
                            return;
                        }

                        moved.Add(LevelBindings.Describe(element));
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        t.RollBack();
                        return;
                    }
                }

                foreach (var element in topElements)
                {
                    var offset = LevelBindings.Find(element, LevelBindings.Top, level.Id)?.GetOffset(element);
                    if (offset == null || !LevelBindings.Shift(offset, shift))
                        moved.Add(LevelBindings.Describe(element) + ", верх");
                }

                level.Elevation = newElevation;

                if (levelCopy != null)
                {
                    _doc.Regenerate();

                    var leftOnCopy = reattached.Count(p => !p.Set(level.Id));

                    // The copy is deleted only when nothing stands on it, so that no element is deleted with it.
                    if (leftOnCopy == 0)
                        _doc.Delete(levelCopy.Id);
                    else
                        moved.Add($"Остались на уровне «{levelCopy.Name}»: {leftOnCopy}");
                }

                t.Commit();
            }

            if (moved.Count != 0)
                ReportDialog.Show(moved, "Переместились вместе с уровнем");
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();
    }
}
