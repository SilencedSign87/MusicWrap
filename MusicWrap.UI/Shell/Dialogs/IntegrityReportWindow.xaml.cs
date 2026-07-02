using Microsoft.Win32;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.UI.Shell.ViewModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Shell.Dialogs;

public partial class IntegrityReportWindow : Window
{
    private readonly IntegrityReportViewModel _viewModel;

    public IntegrityReportWindow(IntegrityReportViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    public void LoadReport(LibraryIntegrityReport report)
    {
        _viewModel.LoadReport(report);
    }

    private void ComboAction_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo)
            return;

        var track = combo.Tag as MissingTrack;
        if (track is null)
            return;

        var selected = combo.SelectedItem as ComboBoxItem;
        var tag = selected?.Tag as string;

        switch (tag)
        {
            case "Remove":
                track.Resolution = MissingTrackResolution.Remove;
                track.LocatedPath = null;
                break;

            case "Locate":
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

                    // Switch to "Located ✓" display
                    foreach (var item in combo.Items)
                    {
                        if (item is ComboBoxItem ci && ci.Tag is string t && t == "Located")
                        {
                            combo.SelectedItem = ci;
                            break;
                        }
                    }
                }
                else
                {
                    // Revert to previously selected item
                    var revertTag = track.Resolution == MissingTrackResolution.Locate
                        ? "Located" : "Remove";
                    foreach (var item in combo.Items)
                    {
                        if (item is ComboBoxItem ci && ci.Tag is string t && t == revertTag)
                        {
                            combo.SelectedItem = ci;
                            break;
                        }
                    }
                }
                break;

            // "Located" is set programmatically only — no action needed
        }
    }
}
