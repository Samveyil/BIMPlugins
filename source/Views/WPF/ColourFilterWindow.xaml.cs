using System.Windows;

namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для ColourFilterWindow.xaml
    /// </summary>
    public partial class ColourFilterWindow : Window
    {
        public ColourFilterWindow(ColourFilterVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
