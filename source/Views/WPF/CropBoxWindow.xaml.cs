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

            // The shapes are temporary: they go away with the window.
            Closed += (s, e) => vm.DeleteShapesEvent.Raise();
        }
    }
}
