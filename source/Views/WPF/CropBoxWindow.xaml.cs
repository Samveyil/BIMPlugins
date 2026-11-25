using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage;
using System;
using System.Windows;


namespace BIMPlugins.Views.WPF
{
    /// <summary>
    /// Логика взаимодействия для CropBoxWindow.xaml
    /// </summary>
    public partial class CropBoxWindow : Window
    {
        public CropBoxWindow(CropBoxVM vm)
        {
            InitializeComponent();
            DataContext = vm;

            Closed += CropBoxWindow_Closed;
        }

        private void CropBoxWindow_Closed(object sender, EventArgs e)
        {
            (DataContext as CropBoxVM).DeleteDirectShapesExEvent.Raise();
        }
    }
}
