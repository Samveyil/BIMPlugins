namespace BIMPlugins.Levels.WPF
{
    public partial class SetNewLevelView
    {
        public SetNewLevelView()
        {
            InitializeComponent();
            DataContext = new SetNewLevelVM();
        }
    }
}
