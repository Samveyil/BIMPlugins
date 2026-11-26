using System.Windows;

namespace BIMPlugins.Parameters.WPF
{
    public partial class SetParameterValueWindow : Window
    {
        public SetParameterValueWindow(SetParameterValueVM vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.CloseRequest += (s, e) => Close();
        }
    }
}
