using BIMPlugins.Windows;
using System;
using System.Threading;
using System.Threading.Tasks;

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
                        var result = MessageDialog.Show(message, buttons: MessageButtons.YesNo, icon: MessageIcon.Information);
                        tcs.SetResult(result == MessageResult.Yes);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }, null);
            }
            else
            {
                var result = MessageDialog.Show(message, buttons: MessageButtons.YesNo, icon: MessageIcon.Information);
                tcs.SetResult(result == MessageResult.Yes);
            }

            return tcs.Task;
        }

        public void ShowError(string message)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(_ =>
                {
                    MessageDialog.Show(message, icon: MessageIcon.Error);
                }, null);
            }
            else
            {
                MessageDialog.Show(message, icon: MessageIcon.Error);
            }
        }
    }
}