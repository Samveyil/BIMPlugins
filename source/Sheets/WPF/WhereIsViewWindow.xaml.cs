using System.Windows;

namespace BIMPlugins.Sheets.WPF
{
    /// <summary>
    /// Логика взаимодействия для WhereIsViewWindow.xaml
    /// </summary>
    public partial class WhereIsViewWindow : Window
    {
        public WhereIsViewWindow(WhereIsViewVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
