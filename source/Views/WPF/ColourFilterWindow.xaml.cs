using System.Windows;

namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для ColourFilterWindow.xaml
    /// </summary>
    public partial class ColourFilterWindow : Window
    {
        public ColourFilterWindow()
        {
            InitializeComponent();

            var vm = new ColourFilterVM();
            vm.CloseRequest += (s, e) => Close();
            DataContext = vm;
        }
    }
}
