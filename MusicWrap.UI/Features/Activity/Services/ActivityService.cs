using MusicWrap.Core.Threading;
using MusicWrap.UI.Features.Activity.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace MusicWrap.UI.Features.Activity.Services
{
    public class ActivityService
    {
        private readonly IUIDispatcher _dispatcher;
        private readonly Dictionary<string, CancellationTokenSource> _cancellationTokens = new();
        public ObservableCollection<ActivityModel> Activities { get; } = new();

        public ActivityService(IUIDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public ActivityScope Start(string title, string description, bool cancellable = true)
        {
            var id = Guid.NewGuid().ToString("N");
            var cts = cancellable ? new CancellationTokenSource() : null;
            var activity = new ActivityModel(id, title, description, cancellable);
            _dispatcher.Invoke(() => Activities.Insert(0, activity));
            if (cts is not null)
                _cancellationTokens[id] = cts;
            return new ActivityScope(activity, cts, this);
        }

        public void Cancel(string activityId)
        {
            if (_cancellationTokens.TryGetValue(activityId, out var cts))
            {
                cts.Cancel();
                _dispatcher.Invoke(() =>
                {
                    var activity = Activities.FirstOrDefault(a => a.Id == activityId);
                    activity?.MarkCancelled();
                });
            }
        }

        public void ClearDone()
        {
            _dispatcher.Invoke(() =>
            {
                var done = Activities.Where(a => a.IsCompleted).ToArray();
                foreach (var a in done)
                {
                    _cancellationTokens.Remove(a.Id);
                    Activities.Remove(a);
                }
            });
        }

        public void ClearOldActivities(TimeSpan? maxAge = null)
        {
            var cutoff = DateTime.Now - (maxAge ?? TimeSpan.FromMinutes(5));
            _dispatcher.Invoke(() =>
            {
                var old = Activities.Where(a => a.CreatedAt < cutoff).ToArray();
                foreach (var a in old)
                {
                    _cancellationTokens.Remove(a.Id);
                    Activities.Remove(a);
                }
            });
        }

        internal void ReleaseCancellation(string activityId)
        {
            _cancellationTokens.Remove(activityId);
        }

    }
    public sealed class ActivityScope : IDisposable
    {
        public ActivityModel Activity { get; }
        public CancellationToken? CancellationToken => _cts?.Token;
        private readonly CancellationTokenSource? _cts;
        private readonly ActivityService _service;
        
        private bool _disposed;

        internal ActivityScope(ActivityModel activity, CancellationTokenSource? cts, ActivityService service)
        {
            Activity = activity;
            _cts = cts;
            _service = service;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            if (Activity.Status == ActivityStatus.Running)
                Activity.Complete();

            _cts?.Dispose();
            _service.ReleaseCancellation(Activity.Id);
        }
    }
}
