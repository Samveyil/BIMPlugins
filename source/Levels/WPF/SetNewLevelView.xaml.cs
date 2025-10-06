namespace BIMPlugins.Levels.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class SetNewLevelView
    {
        public SetNewLevelView(SetNewLevelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
