namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для SumView.xaml
    /// </summary>
    public partial class SumView
    {
        public SumView()
        {
            InitializeComponent();
            DataContext = new SumVM();
        }
    }
}
