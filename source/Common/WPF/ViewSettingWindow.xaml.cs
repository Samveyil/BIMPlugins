using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using System;
using System.Windows.Controls;


namespace BIMPlugins.Common.WPF
{
    /// <summary>
    /// Логика взаимодействия для ViewSettingWindow.xaml
    /// </summary>
    public partial class ViewSettingWindow : UserControl, IDockablePaneProvider
    {
        private readonly UIControlledApplication uiControlApp;

        public ViewSettingWindow(ViewSettingsViewModel viewModel, UIControlledApplication uIApp)
        {
            InitializeComponent();
            DataContext = viewModel;

            uiControlApp = uIApp;
            uiControlApp.DockableFrameVisibilityChanged += new EventHandler<DockableFrameVisibilityChangedEventArgs>(UIApp_DockableFrameVisibilityChanged);
        }

        private void UIApp_DockableFrameVisibilityChanged(object sender, DockableFrameVisibilityChangedEventArgs e)
        {
            if (e.PaneId.Guid == PaneId.Guid)
            {
                if (e.DockableFrameShown)
                {
                    SubscribeToViewEvents();
                    (DataContext as ViewSettingsViewModel).GetViewFilters();
                    (DataContext as ViewSettingsViewModel).GetViewWorksets();
                }
                else
                {
                    UnsubscribeToViewEvents();
                }
            }
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.VisibleByDefault = false;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }
        public static DockablePaneId PaneId
        {
            get
            {
                return new DockablePaneId(new Guid("7C1430C2-03B8-4026-826A-DC542003CD8E"));
            }
        }

        private void SubscribeToViewEvents()
        {
            uiControlApp.ViewActivated += new EventHandler<ViewActivatedEventArgs>(UIApplication_ViewActivated);
            uiControlApp.ControlledApplication.DocumentChanged += new EventHandler<DocumentChangedEventArgs>(Application_DocumentChanged);
            uiControlApp.ControlledApplication.FailuresProcessing += new EventHandler<FailuresProcessingEventArgs>(Application_FailuresProcessing);
        }
        private void UnsubscribeToViewEvents()
        {
            uiControlApp.ViewActivated -= new EventHandler<ViewActivatedEventArgs>(UIApplication_ViewActivated);
            uiControlApp.ControlledApplication.DocumentChanged -= new EventHandler<DocumentChangedEventArgs>(Application_DocumentChanged);
            uiControlApp.ControlledApplication.FailuresProcessing -= new EventHandler<FailuresProcessingEventArgs>(Application_FailuresProcessing);
        }

        private void UIApplication_ViewActivated(object sender, ViewActivatedEventArgs e)
        {
            if (e.Status == RevitAPIEventStatus.Succeeded)
            {
                try
                {
                    (DataContext as ViewSettingsViewModel).GetViewFilters();
                    (DataContext as ViewSettingsViewModel).GetViewWorksets();
                }
                catch { }
                
            }
        }
        private void Application_DocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                if (e.Operation == UndoOperation.TransactionCommitted &&
                    (e.GetTransactionNames().Contains("Фильтр") || e.GetTransactionNames().Contains("Свойства временного вида")))
                {
                    (DataContext as ViewSettingsViewModel).GetViewFilters();
                    (DataContext as ViewSettingsViewModel).GetViewWorksets();
                }
                else if (e.Operation == UndoOperation.TransactionUndone || e.Operation == UndoOperation.TransactionRedone)
                {
                    (DataContext as ViewSettingsViewModel).GetViewFilters();
                    (DataContext as ViewSettingsViewModel).GetViewWorksets();
                }
            }
            catch { }
        }
        private void Application_FailuresProcessing(object sender, FailuresProcessingEventArgs e)
        {
            try
            {
                FailuresAccessor failuresAccessor = e.GetFailuresAccessor();
                if (failuresAccessor.GetTransactionName() == "Активация видового экрана")
                {
                    (DataContext as ViewSettingsViewModel).GetViewFilters();
                    (DataContext as ViewSettingsViewModel).GetViewWorksets();
                }
            }
            catch { }
        }
    }
}
