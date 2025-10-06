namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class SumView
    {
        public SumView(SumViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
