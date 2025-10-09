using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using System.Collections.ObjectModel;
using System.Windows;
using BIMPlugins.ExtStorage.Methods;
using BIMPlugins.Bars;
using System.Linq;
using System;


namespace BIMPlugins.Views.WPF
{
    public partial class SectionBoxViewModel : ObservableObject
    {
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private string _topLevel;
        [ObservableProperty][NotifyCanExecuteChangedFor(nameof(RunCommand))] private string _bottomLevel;
        [ObservableProperty] private double _bottomOffset;
        [ObservableProperty] private double _topOffset;

        private ExternalEvent ExEvent { get; set; }

        public SectionBoxViewModel()
        {
            foreach (Level level in new FilteredElementCollector(RevitAPI.Document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(x => x.Elevation)
                .ToList())
            {
                Levels.Add(level.Name);
            }

            var handler = new RevitAPI.MyEventHandler<SectionBoxViewModel>(
                this,
                vm => vm.SetSectionBox()
            );
            ExEvent = ExternalEvent.Create(handler);
        }

        public ObservableCollection<string> Levels { get; set; } = [];

        [RelayCommand(CanExecute = nameof(CanSet))]
        private void Run() => ExEvent.Raise();

        private void SetSectionBox()
        {
            try
            {
                var doc = RevitAPI.Document;

                double bottomLevel = 0;
                double topLevel = 1;
                foreach (Level level in new FilteredElementCollector(doc).OfClass(typeof(Level)).ToElements().Cast<Level>())
                {
                    if (level.Name == BottomLevel)
                    {
                        bottomLevel = level.ProjectElevation + UnitUtils.ConvertToInternalUnits(BottomOffset, ParameterMethods.GetUnitType());
                    }
                    if (level.Name == TopLevel)
                    {
                        topLevel = level.ProjectElevation + UnitUtils.ConvertToInternalUnits(TopOffset, ParameterMethods.GetUnitType());
                    }
                }

                if (bottomLevel >= topLevel)
                {
                    MessageBox.Show("Отметка низа выше отметки верха!", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                View3D defaultView3D = null;

                if (doc.ActiveView is View3D)
                {
                    defaultView3D = doc.ActiveView as View3D;
                }
                else
                {
                    if (doc.IsWorkshared)
                    {
                        defaultView3D = new FilteredElementCollector(doc).OfClass(typeof(View3D)).ToElements()
                            .Where(e => e.Name == "{3D - " + RevitAPI.Application.Username + "}").FirstOrDefault() as View3D;

                        if (defaultView3D == null)
                        {
                            defaultView3D = Create3DView(doc);
                        }
                    }
                    else
                    {
                        defaultView3D = new FilteredElementCollector(doc).OfClass(typeof(View3D)).ToElements()
                            .Where(e => e.Name == "{3D}").FirstOrDefault() as View3D;

                        if (defaultView3D == null)
                        {
                            defaultView3D = Create3DView(doc);
                        }
                    }

                    UIDocument uidoc = new UIDocument(doc);
                    uidoc.ActiveView = defaultView3D;
                }

                using (Transaction t = new Transaction(doc, "Граница 3Д вида"))
                {
                    t.Start();

                    defaultView3D.IsSectionBoxActive = true;

                    var bb = defaultView3D.GetSectionBox();

                    bb.Min = new XYZ(bb.Min.X, bb.Min.Y, bottomLevel - bb.Transform.Origin.Z);
                    bb.Max = new XYZ(bb.Max.X, bb.Max.Y, topLevel - bb.Transform.Origin.Z);

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

        private static View3D Create3DView(Document doc)
        {
            var viewTypeId = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional)
                .Id;

            View3D view3D;
            using (Transaction t = new Transaction(doc, "Создание 3D вида"))
            {
                t.Start();

                view3D = View3D.CreateIsometric(doc, viewTypeId);
                view3D.Name = "3D";

                t.Commit();
            }

            return view3D;
        }

        private bool CanSet() => !string.IsNullOrEmpty(BottomLevel) && !string.IsNullOrEmpty(TopLevel);
    }
}
