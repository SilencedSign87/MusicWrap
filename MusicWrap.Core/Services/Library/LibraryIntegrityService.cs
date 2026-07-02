using Microsoft.Extensions.Logging;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using System.IO;

namespace MusicWrap.Core.Services.Library
{
    public interface ILibraryIntegrityService
    {
        Task<LibraryIntegrityReport> VerifyAsync(
        IProgress<LibraryVerificationProgress>? progress = null,
        CancellationToken cancellationToken = default);

        Task ApplyFixesAsync(
            LibraryIntegrityReport report,
            CancellationToken cancellationToken = default);

    }

    public class LibraryIntegrityService : ILibraryIntegrityService
    {
        private readonly MusicLibrary _library;
        private readonly ILibraryService _libraryService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILogger _logger;

        public LibraryIntegrityService(MusicLibrary library, ILibraryService libraryService, ISaveCoordinator saveCoordinator, ILogger<LibraryIntegrityService> logger)
        {
            _library = library;
            _libraryService = libraryService;
            _saveCoordinator = saveCoordinator;
            _logger = logger;
        }

        public async Task<LibraryIntegrityReport> VerifyAsync(IProgress<LibraryVerificationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var report = new LibraryIntegrityReport
            {
                GeneratedAt = DateTime.UtcNow,
                TotalTracksChecked = _library.Tracks.Count,
                TotalDirectoriesChecked = _library.Directories?.Count ?? 0,
            };

            // Phase 1 Check files

            progress?.Report(new LibraryVerificationProgress
            {
                TotalTracks = _library.Tracks.Count,
                ProcessedFiles = 0,
                CurrentFile = "Checking track files...",
                Stage = VerificationStage.CheckingFiles
            });

            var missingFingerprints = new Dictionary<(long size, long ticks), Track>();
            int processed = 0;

            foreach (var track in _library.Tracks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (track.Origin != TrackOrigin.Local || string.IsNullOrEmpty(track.Path))
                    continue;

                progress?.Report(new LibraryVerificationProgress
                {
                    TotalTracks = _library.Tracks.Count,
                    ProcessedFiles = ++processed,
                    CurrentFile = track.Path,
                    Stage = VerificationStage.CheckingFiles
                });

                if (File.Exists(track.Path)) continue;

                var key = (track.FileSize, track.LastWriteTime);

                if (!missingFingerprints.ContainsKey(key))
                    missingFingerprints[key] = track;

            }

            _logger.LogInformation("{Total} tracks checked, {Missing} missing files found.", processed, missingFingerprints.Count);

            // Phase 2 Search for moved files

            if (missingFingerprints.Count > 0 && _library.Directories?.Count > 0)
            {
                progress?.Report(new LibraryVerificationProgress
                {
                    TotalTracks = _library.Tracks.Count,
                    ProcessedFiles = processed,
                    CurrentFile = "Searching for moved files...",
                    Stage = VerificationStage.CheckingFiles
                });

                var fingerprintsToFind = new HashSet<(long Size, long Ticks)>(missingFingerprints.Keys);

                foreach (var dir in _library.Directories)
                {
                    if (string.IsNullOrWhiteSpace(dir.Path) || !Directory.Exists(dir.Path))
                        continue;

                    _logger.LogDebug("Searching for moved files in: {path}", dir.Path);

                    try
                    {
                        var searchOption = dir.Recursive
                            ? SearchOption.AllDirectories
                            : SearchOption.TopDirectoryOnly;

                        var audioFiles = Directory
                            .EnumerateFiles(dir.Path, "*", new EnumerationOptions
                            {
                                RecurseSubdirectories = dir.Recursive,
                                IgnoreInaccessible = true,
                            }).Where(f => MusicWrapConstants.SupportedExtensions.Contains(Path.GetExtension(f)));

                        foreach (var file in audioFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            try
                            {
                                var fileInfo = new FileInfo(file);
                                var fingerPrint = (fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks);

                                if (fingerprintsToFind.Remove(fingerPrint))
                                {
                                    // Auto fix path
                                    var track = missingFingerprints[fingerPrint];
                                    var oldPath = track.Path;
                                    track.Path = file;

                                    var moved = new MovedTrack
                                    {
                                        TrackId = track.Id,
                                        OldPath = oldPath,
                                        NewPath = file,
                                        Title = track.Title,
                                        ArtistNames = _libraryService.GetArtistNamesForTrack(track.Id),
                                        AlbumName = _libraryService.GetAlbumById(track.AlbumId)?.Title ?? "UnKnown Album",
                                        AutoFixed = true
                                    };

                                    report.MovedTracks.Add(moved);

                                    _logger.LogInformation(
                                        "Track auto-relocated: \"{Title}\" {OldPath} → {NewPath}",
                                        track.Title, oldPath, file);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error occurred while processing file: {File}", file);
                            }

                            if (fingerprintsToFind.Count == 0) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error occurred while searching in directory: {Directory}", dir.Path);
                    }

                    if (fingerprintsToFind.Count == 0) break;
                }

                // Rename unmatched fingerprints to missing files
                foreach (var kvp in missingFingerprints)
                {
                    if (fingerprintsToFind.Contains(kvp.Key))
                    {
                        var track = kvp.Value;
                        report.MissingTracks.Add(new MissingTrack
                        {
                            TrackId = track.Id,
                            ExpectedPath = track.Path,
                            Title = track.Title,
                            ArtistNames = _libraryService.GetArtistNamesForTrack(track.Id),
                            AlbumName = _libraryService.GetAlbumById(track.AlbumId)?.Title ?? "Unknown Album"
                        });
                    }
                }
            }
            else if (missingFingerprints.Count > 0)
            {
                // no directories to search, mark all missing
                foreach (var kvp in missingFingerprints)
                {
                    var track = kvp.Value;
                    report.MissingTracks.Add(new MissingTrack
                    {
                        TrackId = track.Id,
                        ExpectedPath = track.Path,
                        Title = track.Title,
                        ArtistNames = _libraryService.GetArtistNamesForTrack(track.Id),
                        AlbumName = _libraryService.GetAlbumById(track.AlbumId)?.Title ?? "Unknown Album"
                    });
                }
            }

            // Phase 3 Detect orphaned covers
            progress?.Report(new LibraryVerificationProgress
            {
                TotalTracks = _library.Tracks.Count,
                ProcessedFiles = processed,
                CurrentFile = "Checking for orphaned covers...",
                Stage = VerificationStage.CheckingCovers
            });

            var usedCoverIds = new HashSet<int>(
                _library.Tracks.Select(t => t.CoverId).Where(id => id > 0));

            foreach (var album in _library.Albums)
            {
                if (album.CoverId > 0)
                    usedCoverIds.Add(album.CoverId);
            }

            foreach (var cover in _library.CoverAssets)
            {
                if (!usedCoverIds.Contains(cover.Id))
                {
                    report.OrphanedCovers.Add(new OrphanedCover
                    {
                        CoverId = cover.Id,
                        FileName = cover.FileName
                        // Resolution defaults to CoverResolution.Remove
                    });
                }
            }

            // Phase 4 Detect duplicate tracks

            progress?.Report(new LibraryVerificationProgress
            {
                TotalTracks = _library.Tracks.Count,
                ProcessedFiles = processed,
                CurrentFile = "Checking for duplicate tracks...",
                Stage = VerificationStage.CheckingDuplicates
            });

            var fingerprintGroups = _library.Tracks
                .Where(t => t.Origin == TrackOrigin.Local && t.FileSize > 0)
                .GroupBy(t => (t.FileSize, t.LastWriteTime))
                .Where(g => g.Count() > 1);

            foreach (var group in fingerprintGroups)
            {
                var ordered = group.OrderBy(t => t.Id).ToList();
                var keep = ordered[0];

                foreach (var duplicate in ordered.Skip(1))
                {
                    report.DuplicateTracks.Add(new DuplicateTrack
                    {
                        TrackIdToRemove = duplicate.Id,
                        KeepTrackId = keep.Id,
                        FilePath = duplicate.Path
                        // Resolution defaults to DuplicateResolution.RemoveDuplicate
                    });
                }
            }

            _logger.LogInformation(
                "Integrity check complete: {Moved} auto-fixed, {Missing} missing, {Orphans} orphaned covers, {Dups} duplicates",
                report.MovedTracks.Count,
                report.MissingTracks.Count,
                report.OrphanedCovers.Count,
                report.DuplicateTracks.Count);

            return report;
        }
        public Task ApplyFixesAsync(LibraryIntegrityReport report, CancellationToken cancellationToken = default)
        {
            int removedTracks = 0;
            int relocatedTracks = 0;
            int removedCovers = 0;
            int removedDuplicates = 0;

            // Missing tracks
            foreach (var missing in report.MissingTracks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (missing.Resolution == MissingTrackResolution.Remove)
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == missing.TrackId);
                    if (track is not null)
                    {
                        _library.Tracks.Remove(track);
                        removedTracks++;
                        _logger.LogInformation("Removed missing track: \"{Title}\" ({Path})",
                            missing.Title, missing.ExpectedPath);
                    }
                }
                else if (missing.Resolution == MissingTrackResolution.Locate && !string.IsNullOrWhiteSpace(missing.LocatedPath))
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == missing.TrackId);
                    if (track is not null)
                    {
                        track.Path = missing.LocatedPath;
                        relocatedTracks++;
                        _logger.LogInformation("Relocated missing track: \"{Title}\" → {NewPath}",
                            missing.Title, missing.LocatedPath);
                    }

                }
                // Other ignore
            }

            // Orphaned covers
            foreach (var orphan in report.OrphanedCovers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(orphan.Resolution == CoverResolution.Remove)
                {
                    var cover = _library.CoverAssets.FirstOrDefault(c => c.Id == orphan.CoverId);
                    if (cover is not null)
                    {
                        _library.CoverAssets.Remove(cover);
                        DeleteCoverFiles(cover.FileName);
                        removedCovers++;
                        _logger.LogInformation("Removed orphaned cover: {FileName}", orphan.FileName);
                    }
                }
            }

            // Duplicate tracks
            foreach (var duplicate in report.DuplicateTracks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (duplicate.Resolution == DuplicateResolution.RemoveDuplicate)
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == duplicate.TrackIdToRemove);
                    if (track is not null)
                    {
                        _library.Tracks.Remove(track);
                        removedDuplicates++;
                        _logger.LogInformation("Removed duplicate track: \"{Title}\" ({Path})",
                            track.Title, track.Path);
                    }
                }
            }

            // Persist changes
            if (removedTracks > 0 || relocatedTracks > 0 || removedCovers > 0 || removedDuplicates > 0)
            {
                _saveCoordinator.Enqueue(SaveKind.Library);
                _logger.LogInformation(
                    "Integrity fixes applied: {RemovedTracks} removed, {Relocated} relocated, " +
                    "{Covers} covers cleaned, {Duplicates} duplicates removed",
                    removedTracks, relocatedTracks, removedCovers, removedDuplicates);
            }

            return Task.CompletedTask;
        }

        private static void DeleteCoverFiles(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return;
            try
            {
                var directories = new[]
               {
                    MusicWrapDirectories.CoverDirectory,
                    MusicWrapDirectories.SmallImageDirectory,
                    MusicWrapDirectories.MediumImageDirectory,
                    MusicWrapDirectories.LargeImageDirectory,
                    MusicWrapDirectories.BlurImageDirectory
                };

                foreach (var dir in directories)
                {
                    var path = Path.Combine(dir, fileName);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch
            {
                // Ignore exceptions during file deletion
            }
        }

    }
}
