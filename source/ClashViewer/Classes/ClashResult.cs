using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace BIMPlugins.ClashViewer.Classes
{
    public partial class ClashResult : ObservableObject
    {
        [ObservableProperty] private ObservableCollection<HistoryItem> _historyItems = [];
        [ObservableProperty] private string _status = "Активная";
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private string _lastComment;
        [ObservableProperty] private string _assignedTo;
        [ObservableProperty] private bool? _isLeftSelected = null;
        [ObservableProperty] private SolidColorBrush _color = new SolidColorBrush(Colors.Black);
        [ObservableProperty] private bool _isFixed = false;
        [ObservableProperty] private bool _isModified = false;
        [ObservableProperty] private bool _hasHistoryItems = false;

        private readonly SolidColorBrush _redColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 63, 63));
        private readonly SolidColorBrush _greenColor = new SolidColorBrush(System.Windows.Media.Color.FromRgb(44, 179, 0));

        partial void OnStatusChanged(string value)
        {
            IsActive = value == "Активная";
            HasHistoryItems = HistoryItems.Count != 0;
        }
        partial void OnAssignedToChanged(string value)
        {
            HasHistoryItems = HistoryItems.Count != 0;
        }
        partial void OnIsLeftSelectedChanged(bool? value)
        {
            if (value == null)
            {
                Color = new SolidColorBrush(Colors.Black);
            }
            else
            {
                Color = value == true ? _redColor : _greenColor;
            }
        }
        partial void OnLastCommentChanged(string value)
        {
            HasHistoryItems = HistoryItems.Count != 0;
        }

        public string Name { get; set; }
        public int Number { get; set; }
        public ClashTest Parent { get; set; }
        public string ImagePath { get; set; }
        public XYZ ClashPoint { get; set; }
        public string LevelName { get; set; }
        public string CreatedDate { get; set; }
        public List<ClashObject> ClashObjects { get; set; } = [];
    }
}