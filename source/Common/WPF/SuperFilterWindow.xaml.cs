using System.Windows;

namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для SuperFilterWindow.xaml
    /// </summary>
    public partial class SuperFilterWindow : Window
    {
        public SuperFilterWindow(SuperFilterViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
