using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Queue;
using MusicWrap.Core.Saving;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using MusicWrap.Data.Playlist.Models;
using System.IO;
using System.Net.Sockets;

namespace MusicWrap.Core.Services.Library
{
    public sealed class LibraryVerificationProgress
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
    }
    public interface ILibraryIntegrityService
    {
        void Verify(IProgress<LibraryVerificationProgress>? progress = null, CancellationToken cancellationToken = default);
    }

    public class LibraryIntegrityService : ILibraryIntegrityService
    {
        private readonly MusicLibrary _library;
        private readonly ILibraryService _libraryService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILogger _logger;
        private readonly IMessenger _messenger;

        public LibraryIntegrityService(MusicLibrary library, ILibraryService libraryService, ISaveCoordinator saveCoordinator, ILogger<LibraryIntegrityService> logger, IMessenger messenger)
        {
            _library = library;
            _libraryService = libraryService;
            _saveCoordinator = saveCoordinator;
            _logger = logger;
            _messenger = messenger;
        }

        public void Verify(IProgress<LibraryVerificationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var directories = _library.Directories.ToList();

            List<Track> tracksToRemove = [];
            List<(Track track, string newPath)> tracksToRelocate = [];


            bool hasChanges = false;

            var currentFiles = new Dictionary<(long size, long ticks), string>();

            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
            };

            try
            {
                foreach (var directory in directories)
                {
                    foreach (var file in Directory.EnumerateFiles(directory.Path, "*.*", enumerationOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var fileinfo = new FileInfo(file);
                        currentFiles[(fileinfo.Length, fileinfo.LastWriteTimeUtc.Ticks)] = file;
                    }
                }

                var progressReport = new LibraryVerificationProgress
                {
                    TotalFiles = currentFiles.Count,
                    ProcessedFiles = 0
                };

                foreach (var track in _library.Tracks)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (currentFiles.TryGetValue((track.FileSize, track.LastWriteTime), out var newPath))
                    {
                        if (track.Path != newPath)
                        {
                            tracksToRelocate.Add((track, newPath));
                            hasChanges = true;
                        }
                    }
                    else
                    {
                        tracksToRemove.Add(track);
                        hasChanges = true;
                    }
                }

            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during library verification.");
            }
            finally
            {
                if (hasChanges)
                {
                    _libraryService.ApplyVerification(new TrackVerificationResult
                    {
                        MissingTracks = tracksToRemove,
                        RelocatedTracks = tracksToRelocate
                    }
                    );

                    _saveCoordinator.Enqueue(SaveKind.Library);
                }
            }
        }
    }
    public sealed record TrackVerificationResult
    {
        public required IReadOnlyList<Track> MissingTracks { get; init; }
        public required IReadOnlyList<(Track track, string newPath)> RelocatedTracks { get; init; }
    }
}
