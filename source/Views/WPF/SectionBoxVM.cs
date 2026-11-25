using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using BIMPlugins.Views.Classes;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Views.WPF
{
    public partial class SectionBoxVM : ObservableObject
    {
        private const string Title = "Обрезка по уровням";

        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Level _topLevel;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Level _bottomLevel;
        [ObservableProperty] private double _bottomOffset;
        [ObservableProperty] private double _topOffset;

        private readonly Document _doc;

        private ExternalEvent ExEvent { get; }

        public SectionBoxVM()
        {
            _doc = RevitAPI.Document;

            Levels = _doc.ToElements<Level>()
                .OrderBy(x => x.Elevation)
                .ToList();

            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.SetSectionBox());
        }

        public List<Level> Levels { get; }

        [RelayCommand(CanExecute = nameof(CanSet))]
        private void Run() => ExEvent.Raise();

        private void SetSectionBox()
        {
            try
            {
                var bottomElev = BottomLevel.ProjectElevation + BottomOffset.FromMillimeters();
                var topElev = TopLevel.ProjectElevation + TopOffset.FromMillimeters();

                if (bottomElev >= topElev)
                {
                    MessageWindow.ShowMessage("Отметка низа выше отметки верха!", Title, icon: MessageBoxImage.Warning);
                    return;
                }

                var view3D = ViewGraphics.GetUser3DView(_doc);

                using (Transaction t = new Transaction(_doc, "Граница 3Д вида"))
                {
                    t.Start();

                    view3D.IsSectionBoxActive = true;

                    var bb = view3D.GetSectionBox();
                    bb.Min = bb.Min.SetZ(bottomElev - bb.Transform.Origin.Z);
                    bb.Max = bb.Max.SetZ(topElev - bb.Transform.Origin.Z);
                    view3D.SetSectionBox(bb);

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                MessageWindow.ShowMessage($"{ex.Message}\n{ex.StackTrace}", "Ошибка", icon: MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();

        private bool CanSet() => BottomLevel != null && TopLevel != null;
    }
}
