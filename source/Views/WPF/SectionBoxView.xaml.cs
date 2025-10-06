namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class SectionBoxView
    {
        public SectionBoxView(SectionBoxViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
