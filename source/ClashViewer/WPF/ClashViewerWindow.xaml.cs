using System.Windows;

namespace BIMPlugins.ClashViewer.WPF
{
    /// <summary>
    /// Логика взаимодействия для ClashViewerWindow.xaml
    /// </summary>
    public partial class ClashViewerWindow : Window
    {
        public ClashViewerWindow(ClashViewerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Closing += ClashViewerWindow_Closing;
        }

        private void ClashViewerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            
            (DataContext as ClashViewerViewModel).Dispose();
        }
    }
}
