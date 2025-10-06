using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;

namespace BIMPlugins.ClashViewer.Classes
{
    public partial class ClashTest(string name) : ObservableObject
    {
        [ObservableProperty] private ICollectionView _filteredClashResults;

        public string Name { get; set; } = name;

        public List<ClashResult> ClashResults { get; set; } = [];
    }
}