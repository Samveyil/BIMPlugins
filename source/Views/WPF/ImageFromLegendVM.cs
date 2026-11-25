using Autodesk.Revit.DB;
using BIMPlugins.Bars;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Views.WPF
{
    public partial class ImageFromLegendVM(List<View> legends) : ObservableObject
    {
        [ObservableProperty] private bool _zoom = true;
        [ObservableProperty] private bool _fitToPage = false;
        [ObservableProperty] private int _zoomNumber = 100;
        [ObservableProperty] private int _pixelSize = 512;
        [ObservableProperty] private bool _horizontalFit = true;
        [ObservableProperty] private int _selectedResolution = 600;

        partial void OnZoomChanged(bool value) => FitToPage = !value;

        private readonly List<View> _legends = legends;

        public List<int> Resolutions { get; } = [72, 150, 300, 600];

        [RelayCommand]
        private void Run()
        {
            RaiseCloseRequest();

            var uiDoc = RevitAPI.UIDocument;
            var doc = uiDoc.Document;

            var imageViews = doc.ToElements<ImageView>();

            var exportOptions = new ImageExportOptions
            {
                ExportRange = ExportRange.CurrentView,
                ZoomType = Zoom ? ZoomFitType.Zoom : ZoomFitType.FitToPage,
                FitDirection = HorizontalFit ? FitDirectionType.Horizontal : FitDirectionType.Vertical,
                PixelSize = PixelSize,
                Zoom = ZoomNumber,
                ImageResolution = SelectedResolution switch
                {
                    72 => ImageResolution.DPI_72,
                    150 => ImageResolution.DPI_150,
                    300 => ImageResolution.DPI_300,
                    _ => ImageResolution.DPI_600
                }
            };

            var created = false;
            using (RevitProgressBar revitProgressBar = new RevitProgressBar(true))
            {
                using (TransactionGroup tGroup = new TransactionGroup(doc, "Создание изображений"))
                {
                    tGroup.Start();

                    revitProgressBar.Run("Генерация изображений...", _legends, legend =>
                    {
                        uiDoc.ActiveView = legend;
                        exportOptions.ViewName = legend.Name;

                        using (Transaction t = new Transaction(doc, "Создание изображений"))
                        {
                            t.Start();

                            // An image saved earlier from this legend is replaced.
                            var imageView = imageViews.FirstOrDefault(v => v.Name == legend.Name);
                            if (imageView != null)
                            {
                                imageViews.Remove(imageView);
                                doc.Delete(imageView.Id);
                            }

                            doc.SaveToProjectAsImage(exportOptions);

                            try
                            {
                                legend.ToUIView()?.Close();
                            }
                            catch { }

                            t.Commit();
                        }

                        created = true;
                    });

                    if (revitProgressBar.IsCancelling())
                    {
                        tGroup.RollBack();
                        return;
                    }

                    tGroup.Assimilate();
                }
            }

            if (created)
                MessageDialog.Show("Изображения созданы и добавлены в проект", "Изображения из легенд", icon: MessageIcon.Information);
        }

        public event EventHandler CloseRequest;
        public void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
    }
}
