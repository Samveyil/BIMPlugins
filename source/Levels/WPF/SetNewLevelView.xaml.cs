namespace BIMPlugins.Levels.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class SetNewLevelView
    {
        public SetNewLevelView(SetNewLevelVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
