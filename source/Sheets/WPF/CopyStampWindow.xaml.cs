using System.Windows;

namespace BIMPlugins.Sheets.WPF
{
    /// <summary>
    /// Логика взаимодействия для CopyStampWindow.xaml
    /// </summary>
    public partial class CopyStampWindow : Window
    {
        public CopyStampWindow(CopyStampVM vm)
        {
            InitializeComponent();

            vm.CloseRequest += (s, e) => Close();
            DataContext = vm;
        }
    }
}
