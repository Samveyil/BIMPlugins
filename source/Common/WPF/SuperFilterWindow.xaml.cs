using System.Windows;
using System.Windows.Controls;

namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для SuperFilterWindow.xaml
    /// </summary>
    public partial class SuperFilterWindow : Window
    {
        public SuperFilterWindow()
        {
            InitializeComponent();

            var vm = new SuperFilterVM();
            vm.CloseRequest += (s, e) => Close();
            DataContext = vm;
        }

        // A click on a name expands the item, as the arrow does. SetCurrentValue keeps the IsExpanded binding,
        // so the item still learns that it is expanded and loads its children.
        private void TreeItem_Selected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item)
                item.SetCurrentValue(TreeViewItem.IsExpandedProperty, true);
        }
    }
}
