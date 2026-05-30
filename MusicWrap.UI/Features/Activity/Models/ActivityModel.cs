using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Features.Activity.Models
{
    public enum ActivityStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled
    }
    public partial class ActivityModel : ObservableObject
    {
        public string Id { get; }
        public DateTime CreatedAt { get; }
        public bool IsCancellable { get; }

        [ObservableProperty]
        private string _title;
        [ObservableProperty]
        private string _description;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCancelButton), nameof(IsCompleted))]
        private ActivityStatus _status = ActivityStatus.Running;
        [ObservableProperty]
        private double _progress; // 0.0 - 1.0
        [ObservableProperty]
        private bool _isIndeterminate = true;
        [ObservableProperty]
        private string? _errorMessage;

        public bool ShowCancelButton => IsCancellable && Status == ActivityStatus.Running;
        public bool IsCompleted => Status is ActivityStatus.Completed or ActivityStatus.Cancelled or ActivityStatus.Failed;

        public ActivityModel(string id, string title, string description, bool isCancellable)
        {
            Id = id;
            Title = title;
            Description = description;
            IsCancellable = isCancellable;
            CreatedAt = DateTime.Now;
        }

        public void ReportProgress(double value, string? description = null)
        {
            Progress = Math.Clamp(value, 0, 1);
            IsIndeterminate = false;
            if (description is not null)
                Description = description;
        }
        public void Complete()
        {
            Status = ActivityStatus.Completed;
            Progress = 1.0;
            IsIndeterminate = false;
            Description = "Completed";
        }
        public void Fail(string? error = null)
        {
            Status = ActivityStatus.Failed;
            ErrorMessage = error;
            Description = error ?? "Failed";
            IsIndeterminate = false;
        }
        public void MarkCancelled()
        {
            Status = ActivityStatus.Cancelled;
            IsIndeterminate = false;
            Description = "Cancelled";
        }
    }
}
