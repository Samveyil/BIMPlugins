using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace BIMPlugins.Families.WPF
{
    /// <summary>
    /// Логика взаимодействия для BindParametersWindow.xaml
    /// </summary>
    public partial class BindParametersWindow : Window
    {
        public BindParametersWindow(BindParametersVM vm)
        {
            InitializeComponent();
            DataContext = vm;

            ICollectionView view = CollectionViewSource.GetDefaultView((DataContext as BindParametersVM).Parameters);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription("ParameterGroup"));
        }
    }
}