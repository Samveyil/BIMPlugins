namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для FastSelectView.xaml
    /// </summary>
    public partial class FastSelectView
    {
        public FastSelectView(FastSelectVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
