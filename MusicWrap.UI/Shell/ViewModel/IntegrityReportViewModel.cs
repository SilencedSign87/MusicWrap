using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace MusicWrap.UI.Shell.ViewModel;

public sealed partial class IntegrityReportViewModel : ObservableObject
{
    private readonly ILibraryIntegrityService _integrityService;
    private readonly ISaveCoordinator _saveCoordinator;
    private readonly ILogger _logger;

    [ObservableProperty]
    private LibraryIntegrityReport? _report;

    public ObservableCollection<MissingTrack> MissingTracks { get; } = [];

    [ObservableProperty]
    private bool _removeAllMissing = true;

    [ObservableProperty]
    private int _missingCount;

    [ObservableProperty]
    private int _autoFixedCount;

    [ObservableProperty]
    private string _additionalInfo = string.Empty;

    public int PendingReview => Report?.PendingUserReview ?? 0;
    public bool HasPendingReview => PendingReview > 0;
    public bool HasIssues => Report?.TotalIssues > 0;
    public string SummaryText => Report?.TotalIssues switch
    {
        null => "No report loaded.",
        0 => "All files OK",
        _ => $"{AutoFixedCount} auto-fixed, {PendingReview} pending"
    };

    public IntegrityReportViewModel(
        ILibraryIntegrityService integrityService,
        ISaveCoordinator saveCoordinator,
        ILogger<IntegrityReportViewModel> logger)
    {
        _integrityService = integrityService;
        _saveCoordinator = saveCoordinator;
        _logger = logger;
    }

    public void LoadReport(LibraryIntegrityReport report)
    {
        Report = report;
        MissingTracks.Clear();

        foreach (var m in report.MissingTracks)
        {
            m.Resolution = MissingTrackResolution.Remove;
            MissingTracks.Add(m);
        }

        MissingCount = report.MissingTracks.Count;
        AutoFixedCount = report.AutoFixedCount;

        var extras = new List<string>();
        if (report.OrphanedCovers.Count > 0)
            extras.Add($"{report.OrphanedCovers.Count} cover(s) will be removed");
        if (report.DuplicateTracks.Count > 0)
            extras.Add($"{report.DuplicateTracks.Count} duplicate(s) will be removed");
        AdditionalInfo = extras.Count > 0 ? string.Join(" \u00b7 ", extras) : string.Empty;

        OnPropertyChanged(nameof(PendingReview));
        OnPropertyChanged(nameof(HasPendingReview));
        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(SummaryText));
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (Report is null) return;

        if (RemoveAllMissing)
        {
            foreach (var m in Report.MissingTracks)
                m.Resolution = MissingTrackResolution.Remove;
        }

        try
        {
            await _integrityService.ApplyFixesAsync(Report);
            _logger.LogInformation("Integrity fixes applied");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply fixes");
            MessageBox.Show($"Could not apply fixes:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        foreach (Window w in Application.Current.Windows)
            if (w.DataContext == this) { w.Close(); break; }
    }

    [RelayCommand]
    private void SetMissingRemove(MissingTrack track)
    {
        if (track is null) return;
        track.Resolution = MissingTrackResolution.Remove;
        track.LocatedPath = null;
    }

    [RelayCommand]
    private void LocateMissingTrack(MissingTrack track)
    {
        if (track is null) return;

        var dialog = new OpenFileDialog
        {
            Title = $"Locate: {track.Title}",
            Filter = "Audio files|*.mp3;*.flac;*.wav;*.aac;*.ogg;*.opus;*.m4a|All files|*.*",
            CheckFileExists = true,
            FileName = Path.GetFileName(track.ExpectedPath)
        };

        if (dialog.ShowDialog() == true)
        {
            track.LocatedPath = dialog.FileName;
            track.Resolution = MissingTrackResolution.Locate;
        }
    }

    [RelayCommand]
    private void IgnoreMissingTrack(MissingTrack track)
    {
        if (track is not null) track.Resolution = MissingTrackResolution.Ignore;
    }
}
