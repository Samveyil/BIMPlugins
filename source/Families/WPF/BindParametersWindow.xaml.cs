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
        public BindParametersWindow(BindParametersViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            ICollectionView view = CollectionViewSource.GetDefaultView((DataContext as BindParametersViewModel).Parameters);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription("ParameterGroup"));
        }
    }
}