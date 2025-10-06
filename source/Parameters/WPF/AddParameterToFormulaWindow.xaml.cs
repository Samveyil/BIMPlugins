using System.Windows;

namespace BIMPlugins.Parameters.WPF
{
    /// <summary>
    /// Логика взаимодействия для AddParameterToFormulaWindow.xaml
    /// </summary>
    public partial class AddParameterToFormulaWindow : Window
    {
        public AddParameterToFormulaWindow(AddParameterToFormulaViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
