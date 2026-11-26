namespace BIMPlugins.Parameters.WPF
{
    public partial class NumerateView
    {
        public NumerateView(NumerateVM vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
