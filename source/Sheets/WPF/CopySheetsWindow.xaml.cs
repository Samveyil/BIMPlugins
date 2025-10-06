using System.Windows;


namespace BIMPlugins.Sheets.WPF
{
    /// <summary>
    /// Логика взаимодействия для NumberSheetsWindow.xaml
    /// </summary>
    public partial class CopySheetsWindow : Window
    {
        public CopySheetsWindow(CopySheetsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
