using System.Windows;

namespace BIMPlugins.ClashViewer.WPF
{
    /// <summary>
    /// Логика взаимодействия для UserFilterWindow.xaml
    /// </summary>
    public partial class UserFilterWindow : Window
    {
        public UserFilterWindow(UserFilterVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
