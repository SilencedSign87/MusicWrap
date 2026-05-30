using Microsoft.Extensions.DependencyInjection;
using MusicWrap.UI.Features.Activity.Models;
using MusicWrap.UI.Features.Activity.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MusicWrap.UI.Controls
{
    /// <summary>
    /// Lógica de interacción para ActivityButton.xaml
    /// </summary>
    public partial class ActivityButton : UserControl
    {
        private ActivityService? _activityService;
        public ActivityButton()
        {
            InitializeComponent();

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                Loaded += ActivityButton_Loaded; ;
                Unloaded += ActivityButton_Unloaded; ;
            }
        }

        private void ActivityButton_Loaded(object sender, RoutedEventArgs e)
        {
            _activityService = App.Services.GetService<ActivityService>();
            if (_activityService is null) return;

            UpdateVisualState();

            _activityService.Activities.CollectionChanged += OnActivitiesChanged;

            foreach (var activity in _activityService.Activities)
            {
                activity.PropertyChanged += OnActivityPropertyChanged;
            }
        }
        private void ActivityButton_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_activityService is null) return;
            _activityService.Activities.CollectionChanged -= OnActivitiesChanged;
            foreach (var activity in _activityService.Activities)
            {
                activity.PropertyChanged -= OnActivityPropertyChanged;
            }
            _activityService = null;
        }

        private void OnActivitiesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (ActivityModel activity in e.NewItems)
                    activity.PropertyChanged += OnActivityPropertyChanged;
            }
            if (e.OldItems is not null)
            {
                foreach (ActivityModel activity in e.OldItems)
                    activity.PropertyChanged -= OnActivityPropertyChanged;
            }
            UpdateVisualState();
        }
        private void OnActivityPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActivityModel.Status))
            {
                UpdateVisualState();
            }
        }
        private void UpdateVisualState()
        {
            if (_activityService is null) return;
            int running = _activityService.Activities.Count(a => a.Status == ActivityStatus.Running);
            int total = _activityService.Activities.Count;
            if (running > 0)
            {
                // pending
                IdleIcon.Visibility = Visibility.Collapsed;
                CompletedOverlay.Visibility = Visibility.Collapsed;
                RunningBadge.Visibility = Visibility.Visible;
                CountText.Text = running > 99 ? "99+" : running.ToString();
            }
            else if (total > 0)
            {
                // completed
                IdleIcon.Visibility = Visibility.Collapsed;
                CompletedOverlay.Visibility = Visibility.Visible;
                RunningBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                // default
                IdleIcon.Visibility = Visibility.Visible;
                CompletedOverlay.Visibility = Visibility.Collapsed;
                RunningBadge.Visibility = Visibility.Collapsed;
            }
        }
        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            PART_Popup.IsOpen = !PART_Popup.IsOpen;
        }
    }
}
