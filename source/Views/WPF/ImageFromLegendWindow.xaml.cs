using System.Windows;

namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для ImageFromLegendWindow.xaml
    /// </summary>
    public partial class ImageFromLegendWindow : Window
    {
        public ImageFromLegendWindow(ImageFromLegendVM vm)
        {
            InitializeComponent();

            vm.CloseRequest += (s, e) => Close();
            DataContext = vm;
        }
    }
}
