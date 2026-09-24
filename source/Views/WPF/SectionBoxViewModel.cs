using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Extensions.UtilsExtensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace BIMPlugins.Views.WPF
{
    public partial class SectionBoxViewModel : ObservableObject
    {
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Level _topLevel;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private Level _bottomLevel;
        [ObservableProperty] private double _bottomOffset;
        [ObservableProperty] private double _topOffset;

        private readonly Document _doc = RevitAPI.Document;

        private ExternalEvent ExEvent { get; }

        public SectionBoxViewModel()
        {
            Levels = new (_doc.ToElements<Level>()
                .OrderBy(x => x.Elevation)
                .ToList()
            );

            ExEvent = RevitAPI.CreateExtEvent(this, vm => vm.SetSectionBox());
        }

        public ObservableCollection<Level> Levels { get; set; } = [];

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
                    MessageBox.Show("Отметка низа выше отметки верха!", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                View3D defaultView3D = null;
                if (_doc.ActiveView is View3D view3D)
                {
                    defaultView3D = view3D;
                }
                else
                {
                    var viewName = _doc.IsWorkshared
                        ? "{3D - " + RevitAPI.Application.Username + "}"
                        : "{3D}";

                    defaultView3D = _doc.GetView3D(viewName);
                }

                using (Transaction t = new Transaction(_doc, "Граница 3Д вида"))
                {
                    t.Start();

                    defaultView3D.IsSectionBoxActive = true;

                    var bb = defaultView3D.GetSectionBox();

                    bb.Min = bb.Min.SetZ(bottomElev - bb.Transform.Origin.Z);
                    bb.Max = bb.Max.SetZ(topElev - bb.Transform.Origin.Z);

                    defaultView3D.SetSectionBox(bb);

                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message + ex.StackTrace);
            }
        }

        [RelayCommand]
        private void Close() => RevitOptionsBar.Hide();

        private bool CanSet() => BottomLevel != null && TopLevel != null;
    }
}