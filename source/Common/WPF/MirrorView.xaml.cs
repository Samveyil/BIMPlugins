namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для MirrorView.xaml
    /// </summary>
    public partial class MirrorView
    {
        public MirrorView()
        {
            InitializeComponent();
            DataContext = new MirrorVM();
        }
    }
}
