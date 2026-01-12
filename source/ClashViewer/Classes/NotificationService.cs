using BIMPlugins.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BIMPlugins.ClashViewer.Classes
{
    internal class NotificationService(SynchronizationContext syncContext = null)
    {
        private readonly SynchronizationContext _syncContext = syncContext ?? SynchronizationContext.Current;

        public Task<bool> ShowQuestion(string message)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (_syncContext != null)
            {
                _syncContext.Post(_ =>
                {
                    try
                    {
                        var result = MessageWindow.ShowMessage(message, MessageBoxImage.Information, false);
                        tcs.SetResult(result == MessageBoxResult.Yes);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, null);
            }
            else
            {
                var result = MessageWindow.ShowMessage(message, MessageBoxImage.Information, false);
                tcs.SetResult(result == MessageBoxResult.Yes);
            }

            return tcs.Task;
        }

        public void ShowError(string message)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ =>
                {
                    MessageWindow.ShowMessage(message, MessageBoxImage.Error);
                }, null);
            }
            else
            {
                MessageWindow.ShowMessage(message, MessageBoxImage.Error);
            }
        }
    }
}