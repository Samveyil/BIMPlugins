namespace BIMPlugins.Levels.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class MoveLevelView
    {
        public MoveLevelView(MoveLevelVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
