namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class SectionBoxView
    {
        public SectionBoxView(SectionBoxVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
