namespace BIMPlugins.Parameters.WPF
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
