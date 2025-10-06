using System.Windows;

namespace BIMPlugins.Sheets.WPF
{
    /// <summary>
    /// Логика взаимодействия для SpecificationScheduleWindow.xaml
    /// </summary>
    public partial class SpecificationScheduleWindow : Window
    {
        public SpecificationScheduleWindow(SpecificationScheduleViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
