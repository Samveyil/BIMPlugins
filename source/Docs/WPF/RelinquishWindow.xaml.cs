using System.Windows;

namespace BIMPlugins.Docs.WPF
{
    /// <summary>
    /// Логика взаимодействия для RelinquishWindow.xaml
    /// </summary>
    public partial class RelinquishWindow : Window
    {
        public RelinquishWindow(RelinquishViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
