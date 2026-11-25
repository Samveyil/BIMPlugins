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

        public static readonly DockablePaneId PaneId = new DockablePaneId(new Guid("7C1430C2-03B8-4026-826A-DC542003CD8E"));

        public const string Title = "Управление видимостью";

        private ViewSettingsVM VM { get; } = new();

        public ViewSettingWindow(UIControlledApplication application)
        {
            InitializeComponent();
            DataContext = VM;

            uiControlApp = application;
            uiControlApp.DockableFrameVisibilityChanged += new EventHandler<DockableFrameVisibilityChangedEventArgs>(UIApp_DockableFrameVisibilityChanged);
        }

        private void UIApp_DockableFrameVisibilityChanged(object sender, DockableFrameVisibilityChangedEventArgs e)
        {
            if (e.PaneId.Guid == PaneId.Guid)
            {
                if (e.DockableFrameShown)
                {
                    SubscribeToViewEvents();
                    VM.Refresh();
                }
                else
                {
                    UnsubscribeToViewEvents();
                }
            }
        }

        public static void Register(UIControlledApplication application)
        {
            application.RegisterDockablePane(PaneId, Title, new ViewSettingWindow(application));
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
                    VM.Refresh();
                }
                catch { }
            }
        }
        private void Application_DocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                var names = e.GetTransactionNames();
                var refresh = e.Operation == UndoOperation.TransactionCommitted
                    ? names.Contains("Фильтр") || names.Contains("Свойства временного вида")
                    : e.Operation == UndoOperation.TransactionUndone || e.Operation == UndoOperation.TransactionRedone;

                if (refresh)
                    VM.Refresh();
            }
            catch { }
        }
        private void Application_FailuresProcessing(object sender, FailuresProcessingEventArgs e)
        {
            try
            {
                if (e.GetFailuresAccessor().GetTransactionName() == "Активация видового экрана")
                {
                    VM.Refresh();
                }
            }
            catch { }
        }
    }
}
