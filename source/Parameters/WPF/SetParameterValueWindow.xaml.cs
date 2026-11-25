using System.Windows;

namespace BIMPlugins.Parameters.WPF
{
    /// <summary>
    /// Логика взаимодействия для SetParameterValueWindow.xaml
    /// </summary>
    public partial class SetParameterValueWindow : Window
    {
        public SetParameterValueWindow(SetParameterValueVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
