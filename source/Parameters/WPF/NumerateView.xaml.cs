namespace BIMPlugins.Parameters.WPF
{
    /// <summary>
    /// Логика взаимодействия для OVVKWorksetView.xaml
    /// </summary>
    public partial class NumerateView
    {
        public NumerateView(NumerateVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
