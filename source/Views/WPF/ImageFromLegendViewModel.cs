using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BIMPlugins.ExtStorage;
using System.Windows;
using BIMPlugins.Bars;
using System.Collections.Generic;
using System.Linq;
using System;

namespace BIMPlugins.Views.WPF
{
    public partial class ImageFromLegendViewModel : ObservableObject
    {
        [ObservableProperty] private bool _zoom = true;
        [ObservableProperty] private bool _fitToPage = false;
        [ObservableProperty] private int _zoomNumber = 100;
        [ObservableProperty] private int _pixelSize = 512;
        [ObservableProperty] private bool _horizontalFit = true;
        [ObservableProperty] private int _selectedResolution = 600;

        partial void OnZoomChanged(bool value)
        {
            FitToPage = !value;
        }

        private readonly List<View> _legends = [];

        public List<int> Resolutions { get; set; } = [72, 150, 300, 600];

        public ImageFromLegendViewModel(List<View> legends)
        {
            _legends = legends;
        }


        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();

            var doc = RevitAPI.Document;

            var imageViews = new FilteredElementCollector(doc)
                .OfClass(typeof(ImageView))
                .ToList();

            ImageExportOptions exportOptions = new ImageExportOptions();
            exportOptions.ExportRange = ExportRange.CurrentView;
            
            exportOptions.ZoomType = Zoom
                ? ZoomFitType.Zoom
                : ZoomFitType.FitToPage;
            exportOptions.FitDirection = HorizontalFit
                ? FitDirectionType.Horizontal
                : FitDirectionType.Vertical;
            
            exportOptions.PixelSize = PixelSize;
            exportOptions.Zoom = ZoomNumber;

            ImageResolution resolution;
            switch (SelectedResolution)
            {
                case 72:
                    resolution = ImageResolution.DPI_72;
                    break;
                
                case 150:
                    resolution = ImageResolution.DPI_150;
                    break;
                
                case 300:
                    resolution = ImageResolution.DPI_300;
                    break;
                
                default:
                    resolution = ImageResolution.DPI_600;
                    break;
            }
            exportOptions.ImageResolution = resolution;

            bool flag = false;
            using (RevitProgressBar revitProgressBar = new RevitProgressBar(true))
            {
                using (TransactionGroup tGroup = new TransactionGroup(doc, "Создание изображений"))
                {
                    tGroup.Start();

                    revitProgressBar.Run("Генерация изображений...", _legends, (legend) =>
                    {
                        RevitAPI.UIDocument.ActiveView = legend;

                        exportOptions.ViewName = legend.Name;

                        using (Transaction t = new Transaction(doc, "Создание изображений"))
                        {
                            t.Start();

                            if (imageViews.Select(v => v.Name).Contains(legend.Name))
                            {
                                var imageView = imageViews.FirstOrDefault(v => v.Name == legend.Name);
                                imageViews.Remove(imageView);
                                
                                doc.Delete(imageView.Id);
                            }

                            doc.SaveToProjectAsImage(exportOptions);
                            
                            try
                            {
                                RevitAPI.UIDocument.GetOpenUIViews().FirstOrDefault(v => v.ViewId == legend.Id)?.Close();
                            }
                            catch { }

                            t.Commit();
                        }

                        flag = true;
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        tGroup.RollBack();
                        return;
                    }

                    tGroup.Assimilate();
                }  
            }

            if (flag)
            {
                MessageBox.Show("Изображения созданы и добавлены в проект", "BIMPlugins", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        } 
    }
}