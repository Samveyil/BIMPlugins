namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для RotateView.xaml
    /// </summary>
    public partial class FastSelectView
    {
        public FastSelectView(FastSelectViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
