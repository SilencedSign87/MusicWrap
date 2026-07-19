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
        private readonly PlaylistData _playlistData;
        private readonly IQueueManager _queueManager;
        private readonly ILibraryService _libraryService;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILogger _logger;
        private readonly IMessenger _messenger;

        public LibraryIntegrityService(MusicLibrary library, PlaylistData playlistData, IQueueManager queueManager, ILibraryService libraryService, ISaveCoordinator saveCoordinator, ILogger<LibraryIntegrityService> logger, IMessenger messenger)
        {
            _library = library;
            _playlistData = playlistData;
            _queueManager = queueManager;
            _libraryService = libraryService;
            _saveCoordinator = saveCoordinator;
            _logger = logger;
            _messenger = messenger;
        }

        public void Verify(IProgress<LibraryVerificationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var directories = _library.Directories.ToList();

            List<Track> tracksToRemove = [];

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
                            track.Path = newPath;
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
                    if (tracksToRemove.Count > 0)
                    {
                        _library.Tracks.RemoveAll(t => tracksToRemove.Contains(t));
                    }

                    CleanupLibraryObjects();

                    _messenger.Send(new LibraryChangedMessage(LibraryChangeType.FullReload));

                    _saveCoordinator.Enqueue(SaveKind.Library);
                }
            }
        }
        private void CleanupLibraryObjects()
        {
            _library.Albums.RemoveAll(a => !_library.Tracks.Any(t => t.AlbumId == a.Id));
            _library.Artists.RemoveAll(a => !_library.Albums.Any(al => al.ArtistIds.Contains(a.Id)));
            _library.Artists.RemoveAll(a => !_library.Tracks.Any(t => t.ArtistIds.Contains(a.Id)));
            _library.Genres.RemoveAll(g => !_library.Tracks.Any(t => t.GenreIds.Contains(g.Id)));
            _library.CoverAssets.RemoveAll(c => !_library.Albums.Any(a => a.CoverId == c.Id) && !_library.Tracks.Any(t => t.CoverId == c.Id));

            _playlistData.Playlists.ForEach(p =>
            {
                p.Items.RemoveAll(i => !_library.Tracks.Any(t => t.Id == i.TrackId));
            });

            var queue = _queueManager.Items;
            var indicesToRemove = new List<int>();
            foreach (var item in queue)
            {
                if (item.SourceType == QueueItemSourceType.LocalFile && item.LibraryId is not null)
                {
                    var track = _library.Tracks.FirstOrDefault(t => t.Id == item.LibraryId);
                    if (track == null)
                    {
                        indicesToRemove.Add(queue.IndexOf(item));
                    }
                }
            }
            _queueManager.Remove(indicesToRemove);
        }
    }
}
