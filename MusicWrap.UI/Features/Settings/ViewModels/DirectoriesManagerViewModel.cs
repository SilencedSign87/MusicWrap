using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Services;
using MusicWrap.UI.Features.Library.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Xml.Serialization;
using MusicWrap.Core.Services.Library;

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

        private IProgress<ScanProgress>? _scanningProgress;
        private CancellationTokenSource _scanCancellationTokenSource;

        private readonly ILibraryScanner _scanner;
        private readonly MusicLibrary _library;
        private readonly ILibraryCacheService _cache;

        public DirectoriesManagerViewModel(ILibraryScanner scanner, ILibraryCacheService cache, MusicLibrary library)
        {
            _scanner = scanner;
            _library = library;
            _cache = cache;


            _scanCancellationTokenSource = new CancellationTokenSource();

            _scanningProgress = new Progress<ScanProgress>(progress =>
            {
                TotalFiles = progress.TotalFiles;
                FilesProcessed = progress.FilesProcessed;
                CurrentFile = string.IsNullOrEmpty(progress.CurrentFile)
                    ? string.Empty
                    : System.IO.Path.GetFileName(progress.CurrentFile);
                ProgressPercentage = progress.TotalFiles > 0
                    ? (double)progress.FilesProcessed / progress.TotalFiles * 100
                    : 0;
            });

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
            IsScanning = Visibility.Visible;
            _scanCancellationTokenSource = new CancellationTokenSource();

            try
            {
                var cancelationToken = _scanCancellationTokenSource.Token;

                if (SelectedDirectories.Count > 0)
                {
                    var paths = SelectedDirectories.Select(d => d.Path).ToArray();
                    await _scanner.ScanSpecificDirectories(paths, _scanningProgress, cancelationToken);
                }
                else
                {
                    await _scanner.ScanAllDirectories(_scanningProgress, cancelationToken);
                }
                _cache.InvalidateCache();
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Scan was canceled by user");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during scanning: {ex.Message}");
            }
            finally
            {

                UpdateDirectories();
                IsScanning = Visibility.Hidden;
            }
        }

        [RelayCommand]
        private void CancelScan()
        {
            _scanCancellationTokenSource?.Cancel();
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


        private void UpdateDirectories()
        {
            Directories = [.. _library.Directories];
        }

    }
}



