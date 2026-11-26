using System.Windows;

namespace BIMPlugins.Parameters.WPF
{
    public partial class AddParameterToFormulaWindow : Window
    {
        public AddParameterToFormulaWindow(AddParameterToFormulaVM vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseRequest += (s, e) => Close();
        }
    }
}
