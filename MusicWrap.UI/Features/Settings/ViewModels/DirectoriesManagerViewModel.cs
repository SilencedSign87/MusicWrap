using Acornima.Ast;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Activity.Models;
using MusicWrap.UI.Features.Activity.Services;
using MusicWrap.UI.Shared.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MusicWrap.UI.Features.Settings.ViewModels
{
    public partial class DirectoriesManagerViewModel : ObservableObject
    {
        [ObservableProperty]
        private ScanDirectory[] directories = [];

        [ObservableProperty]
        private List<ScanDirectory> selectedDirectories = [];

        [ObservableProperty]
        private bool hasSelectedDirectories = false;

        [ObservableProperty]
        private Visibility isScanning = Visibility.Hidden;

        [ObservableProperty]
        private int totalFiles = 0;

        [ObservableProperty]
        private int filesProcessed = 0;

        [ObservableProperty]
        private string currentFile = string.Empty;

        [ObservableProperty]
        private double progressPercentage = 0;

        private readonly ILibraryScanner _scanner;
        private readonly ILibraryService _libraryService;
        private readonly ActivityService _activityService;
        private readonly ILibraryIntegrityService _libraryIntegrityService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly WindowManager _windowManager;

        private ActivityScope? _currentScanScope;

        public DirectoriesManagerViewModel(ILibraryScanner scanner, ILibraryService libraryService, ActivityService activityService, ILibraryIntegrityService libraryIntegrityService, ISaveCoordinator saveCoordinator, WindowManager windowManager)
        {
            _scanner = scanner;
            _libraryService = libraryService;
            _activityService = activityService;
            _libraryIntegrityService = libraryIntegrityService;
            _saveCoordinator = saveCoordinator;
            _windowManager = windowManager;

            UpdateDirectories();
        }
        public void SetSelectedDirectories(List<ScanDirectory> selected)
        {
            SelectedDirectories = selected;
            HasSelectedDirectories = selected.Count > 0;
        }

        [RelayCommand]
        private void AddFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Multiselect = true,
                Title = "Select music folders to add",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var directory in dialog.FolderNames)
                {
                    _scanner.AddDirectory(directory, true);
                }

                UpdateDirectories();
            }

        }

        [RelayCommand]
        private void RemoveSelected()
        {
            if (SelectedDirectories.Count <= 0) return;
            foreach (var dir in SelectedDirectories)
            {
                _scanner.RemoveDirectory(dir.Path, true);
            }
        }

        [RelayCommand]
        private async Task ScanSelected()
        {
            _currentScanScope?.Dispose();

            var title = SelectedDirectories.Count > 0
            ? "Scanning directories"
            : "Scanning all directories";
            var description = "Preparing to scan...";

            _currentScanScope = _activityService.Start(title, description, cancellable:true);

            var activity = _currentScanScope.Activity;

            IsScanning = Visibility.Visible;

            try
            {
                var progress = new Progress<ScanProgress>(p =>
                {
                    // Local
                    TotalFiles = p.TotalFiles;
                    FilesProcessed = p.FilesProcessed;
                    CurrentFile = string.IsNullOrEmpty(p.CurrentFile)
                        ? string.Empty
                        : Path.GetFileName(p.CurrentFile);
                    ProgressPercentage = p.TotalFiles > 0
                        ? (double)p.FilesProcessed / p.TotalFiles * 100
                        : 0;

                    //Activity Center
                    var phase = p.State switch
                    {
                        ScanState.Fingerprinting => "Fingerprinting",
                        ScanState.Scanning => "Scanning",
                        ScanState.Saving => "Saving",
                        _ => "Processing"
                    };
                    var detail = string.IsNullOrWhiteSpace(p.CurrentFile)
                        ? phase
                        : $"{phase} — {Path.GetFileName(p.CurrentFile)}";
                    activity.ReportProgress(
                        p.TotalFiles > 0 ? (double)p.FilesProcessed / p.TotalFiles : 0,
                        detail);
                });

                if (SelectedDirectories.Count > 0)
                {
                    var paths = SelectedDirectories.Select(d => d.Path).ToArray();
                    await _scanner.ScanSpecificDirectories(paths, progress, _currentScanScope.CancellationToken);
                }
                else
                {
                    await _scanner.ScanAllDirectories(progress, _currentScanScope.CancellationToken);
                }
                activity.Complete();
            }
            catch (OperationCanceledException)
            {
                if (activity.Status == ActivityStatus.Running)
                    activity.MarkCancelled();
            }
            catch (Exception ex)
            {
                activity.Fail(ex.Message);
            }
            finally
            {

                if (Application.Current?.Dispatcher?.HasShutdownStarted == false)
                {
                    UpdateDirectories();
                    IsScanning = Visibility.Hidden;
                    TotalFiles = 0;
                    FilesProcessed = 0;
                    CurrentFile = string.Empty;
                    ProgressPercentage = 0;
                }
            }
        }

        [RelayCommand]
        private void CancelScan()
        {
            if(_currentScanScope is not null)
            {
                _activityService.Cancel(_currentScanScope.Activity.Id);
            }
        }

        [RelayCommand]
        private void OpenFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private async Task RunIntegrityCheckAsync()
        {
            using var scope = _activityService.Start(
                "Library integrity check",
                "Checking files...",
                cancellable: true);

            var activity = scope.Activity;

            try
            {
                var progress = new Progress<LibraryVerificationProgress>(p =>
                {
                    var desc = p.Stage switch
                    {
                        VerificationStage.CheckingFiles
                            => $"Checking: {Path.GetFileName(p.CurrentFile)}",
                        VerificationStage.CheckingCovers
                            => "Checking orphaned covers...",
                        VerificationStage.CheckingDuplicates
                            => "Checking duplicates...",
                        VerificationStage.AutoFixing
                            => "Auto-fixing moved tracks...",
                        _ => "Working..."
                    };
                    activity.ReportProgress(
                        p.TotalTracks > 0 ? (double)p.ProcessedFiles / p.TotalTracks : 0,
                        desc);
                });

                var report = await _libraryIntegrityService.VerifyAsync(progress, scope.CancellationToken);

                if (report.TotalIssues == 0)
                {
                    activity.Complete();
                    return;
                }

                if (report.PendingUserReview == 0)
                {
                    activity.ReportProgress(1, $"{report.AutoFixedCount} items auto-fixed");
                    _saveCoordinator.Enqueue(SaveKind.Library);
                    activity.Complete();
                    return;
                }

                activity.Complete();
                _windowManager.LaunchIntegrityReportWindow(report);
            }
            catch (OperationCanceledException)
            {
                if (activity.Status == ActivityStatus.Running)
                    activity.MarkCancelled();
            }
            catch (Exception ex)
            {
                activity.Fail(ex.Message);
            }
        }

        private void UpdateDirectories()
        {
            Directories = [.. _libraryService.GetDirectories()];
        }
    }
}



