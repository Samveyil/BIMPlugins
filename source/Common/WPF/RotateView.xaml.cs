namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для RotateView.xaml
    /// </summary>
    public partial class RotateView
    {
        public RotateView(RotateViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
