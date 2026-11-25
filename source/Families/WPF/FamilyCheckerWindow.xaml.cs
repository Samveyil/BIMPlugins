using System.Windows;

namespace BIMPlugins.Families.WPF
{
    /// <summary>
    /// Логика взаимодействия для FamilyCheckerWindow.xaml
    /// </summary>
    public partial class FamilyCheckerWindow : Window
    {
        public FamilyCheckerWindow(FamilyCheckerVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
