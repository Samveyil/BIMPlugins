using System.Windows;

namespace BIMPlugins.Test2dRebar.WPF
{
    /// <summary>
    /// Логика взаимодействия для PalkaViewManagerWindow.xaml
    /// </summary>
    public partial class PalkaViewManagerWindow : Window
    {
        public PalkaViewManagerWindow(PalkaViewManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
