using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Activity;
using System.Collections.ObjectModel;

namespace MusicWrap.UI.Features.Activity.Viewmodel
{
    public partial class ActivityCenterViewModel : ObservableObject
    {
        private readonly ActivityService _activityService;

        public ReadOnlyObservableCollection<ActivityModel> Activities { get; }

        public ActivityCenterViewModel(ActivityService activityService)
        {
            _activityService = activityService;
            Activities = new ReadOnlyObservableCollection<ActivityModel>(_activityService.Activities);
        }

        [RelayCommand]
        private void Refresh()
        {
            _activityService.ClearOldActivities();
        }

        [RelayCommand]
        private void ClearDone()
        {
            _activityService.ClearDone();
        }

        [RelayCommand]
        private void CancelActivity(ActivityModel? activity)
        {
            if (activity is not null && activity.IsCancellable && activity.Status == ActivityStatus.Running)
            {
                _activityService.Cancel(activity.Id);
            }
        }
    }
}
