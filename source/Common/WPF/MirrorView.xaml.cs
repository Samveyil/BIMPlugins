namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class MirrorView
    {
        public MirrorView(MirrorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
