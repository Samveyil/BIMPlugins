using System.Windows;

namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для CropBoxWindow.xaml
    /// </summary>
    public partial class CropBoxWindow : Window
    {
        public CropBoxWindow()
        {
            InitializeComponent();

            var vm = new CropBoxVM();
            DataContext = vm;

            // The ranges are shown while the window is open.
            Closed += (s, e) => vm.HideAll();
        }
    }
}
