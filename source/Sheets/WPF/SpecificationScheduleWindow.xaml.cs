using System.Windows;

namespace BIMPlugins.Sheets.WPF
{
    /// <summary>
    /// Логика взаимодействия для SpecificationScheduleWindow.xaml
    /// </summary>
    public partial class SpecificationScheduleWindow : Window
    {
        public SpecificationScheduleWindow(SpecificationScheduleVM vm)
        {
            InitializeComponent();

            vm.CloseRequest += (s, e) => Close();
            DataContext = vm;
        }
    }
}
