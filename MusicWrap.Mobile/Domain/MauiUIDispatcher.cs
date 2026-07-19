using MusicWrap.Core.Threading;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Mobile.Domain
{
    public class MauiUIDispatcher : IUIDispatcher
    {
        public bool CanAccess() => MainThread.IsMainThread;

        public void Invoke(Action action)
        {
            if (CanAccess())
                action();
            else
                MainThread.BeginInvokeOnMainThread(action);
        }

        public Task InvokeAsync(Action action)
        {
            var tcs = new TaskCompletionSource();
            if (MainThread.IsMainThread)
            {
                try { action(); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try { action(); tcs.SetResult(); }
                    catch (Exception ex) { tcs.SetException(ex); }
                });
            }
            return tcs.Task;
        }
    }
}
