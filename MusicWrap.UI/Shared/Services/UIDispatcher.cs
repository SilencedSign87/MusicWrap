using Microsoft.Extensions.Logging;
using MusicWrap.Core.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace MusicWrap.UI.Shared.Services
{
    public class UIDispatcher : IUIDispatcher
    {
        private readonly ILogger _logger;
        public UIDispatcher(ILogger<UIDispatcher> logger)
        {
            _logger = logger;
        }
        public bool CanAccess()
        {
            return Application.Current?.Dispatcher?.CheckAccess() ?? true;
        }

        public void Invoke(Action action)
        {
            try
            {

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    action();
                    return;
                }
                dispatcher.Invoke(action);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invoking action on UI thread");
            }
        }

        public Task InvokeAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            return dispatcher.InvokeAsync(action).Task;
        }
    }
}
