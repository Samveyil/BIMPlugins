using System.Windows;
using System.Windows.Controls;

namespace BIMPlugins.Sheets.WPF
{
    /// <summary>
    /// Логика взаимодействия для NumberSheetsWindow.xaml
    /// </summary>
    public partial class SheetsNumberingWindow : Window
    {
        public SheetsNumberingWindow(SheetsNumberingVM vm)
        {
            InitializeComponent();

            vm.CloseRequest += (s, e) => Close();
            DataContext = vm;
        }

        private void ComboBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            (sender as ComboBox).IsDropDownOpen = true;
        }
    }
}
