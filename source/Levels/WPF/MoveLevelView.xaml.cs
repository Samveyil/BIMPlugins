namespace BIMPlugins.Levels.WPF
{
    public partial class MoveLevelView
    {
        public MoveLevelView()
        {
            InitializeComponent();
            DataContext = new MoveLevelVM();
        }
    }
}
