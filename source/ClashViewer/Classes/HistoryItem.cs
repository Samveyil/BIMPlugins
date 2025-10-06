using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace BIMPlugins.ClashViewer.Classes
{
    public partial class HistoryItem : ObservableObject
    {
        [ObservableProperty] private string _comment;
        [ObservableProperty] private string _author;
        [ObservableProperty] private string _chipIcon = string.Empty;

        partial void OnAuthorChanged(string value)
        {
            ChipIcon += value[0];

            int firstDotIndex = value.IndexOf('.');
            if (firstDotIndex != -1 && firstDotIndex + 1 < value.Length)
            {
                ChipIcon += value[firstDotIndex + 1];
            }
            ChipIcon = ChipIcon.ToUpper();
        }

        public HistoryItem(string comment)
        {
            Comment = comment;
        }
        public HistoryItem(string oldStatus, string newStatus)
        {
            Comment = $"Статус: {oldStatus} => {newStatus}";
            OldStatus = oldStatus;
        }

        public string Date { get; set; } = DateTime.Now.ToString("dd-MM-yyyy HH:mm");
        public string OldStatus { get; set; }
        public string OldRole { get; set; }
    }
}