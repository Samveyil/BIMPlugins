namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class NumerateView
    {
        public NumerateView(NumerateViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
