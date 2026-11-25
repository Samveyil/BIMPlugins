namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для SectionBoxView.xaml
    /// </summary>
    public partial class SectionBoxView
    {
        public SectionBoxView()
        {
            InitializeComponent();
            DataContext = new SectionBoxVM();
        }
    }
}
