using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using BIMPlugins.ExtStorage.Extensions;

namespace BIMPlugins.Sheets.Classes
{
    public partial class ViewItem : ObservableObject
    {
        [ObservableProperty] private bool _isSelected = true;

        public string Name { get; set; }
        public ViewType ViewType { get; set; }
        public SheetCopyItem Parent { get; set; }
        public Viewport Viewport { get; set; }
        public View View { get; set; }
        public ScheduleSheetInstance ScheduleSheetInstance { get; set; }
        public XYZ CenterPoint { get; set; }

        public ViewItem(Viewport viewport)
        {
            Viewport = viewport;
            View = viewport.ViewId.ToElement<View>();

            Name = View.Title;
            ViewType = View.ViewType;
        }
        public ViewItem(ScheduleSheetInstance scheduleSheetInstance)
        {
            ScheduleSheetInstance = scheduleSheetInstance;

            Name = "Спецификация: " + ScheduleSheetInstance.Name;
            ViewType = ViewType.Schedule;
            CenterPoint = ScheduleSheetInstance.Point;
        }
    }
}