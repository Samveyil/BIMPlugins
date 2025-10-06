using System.Windows;

namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для ColourFilterWindow.xaml
    /// </summary>
    public partial class ColourFilterWindow : Window
    {
        public ColourFilterWindow(ColourFilterViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
