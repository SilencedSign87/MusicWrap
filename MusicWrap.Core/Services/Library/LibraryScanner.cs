using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using System.IO;

namespace MusicWrap.Core.Services.Library
{
    public interface ILibraryScanner
    {
        Task ScanAllDirectories(IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        Task ScanSpecificDirectories(string[] paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        Task ScanDirectory(string path, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        Task ScanFiles(string[] paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        void AddDirectory(string path, bool recursive);
        void RemoveDirectory(string path, bool keepTracks);
        IReadOnlyList<ScanDirectory> GetDirectories();
    }
    public class LibraryScanner : ILibraryScanner
    {
        private readonly MusicLibrary _library;
        private readonly ILibraryRepository _store;
        private readonly ILibraryIndexer _indexer;

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".flac", ".wav", ".aac", ".ogg", ".opus", ".m4a"
        };

        private List<ScanDirectory> Directories
        {
            get
            {
                _library.Directories ??= new List<ScanDirectory>();
                return _library.Directories;
            }
        }

        public LibraryScanner(MusicLibrary library, ILibraryRepository store, ILibraryIndexer indexer)
        {
            _library = library;
            _store = store;
            _indexer = indexer;
        }
        public async Task ScanAllDirectories(IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            var allFiles = new List<string>();
            foreach (var dir in Directories)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                allFiles.AddRange(
                    GetFilesFromDirectory(dir.Path, dir.Recursive)
                );

                dir.LastScan = DateTime.UtcNow;
            }

            await ScanFiles([.. allFiles], progress, cancellationToken);

            _store.Save(_library);
        }

        public async Task ScanSpecificDirectories(string[] paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            var allFiles = new List<string>();

            foreach (var path in paths)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                var dir = Directories.FirstOrDefault(d =>
                    string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));

                if (dir is null) continue;

                allFiles.AddRange(
                    GetFilesFromDirectory(dir.Path, dir.Recursive)
                );

                dir.LastScan = DateTime.UtcNow;
            }

            await ScanFiles([.. allFiles], progress, cancellationToken);

            _store.Save(_library);
        }

        public void AddDirectory(string path, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));
            }
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
            }
            var existing = Directories.FirstOrDefault(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
                existing.Recursive = recursive;
            else
                Directories.Add(new ScanDirectory { Path = path, Recursive = recursive });

            _store.Save(_library);
        }

        public IReadOnlyList<ScanDirectory> GetDirectories() => Directories.AsReadOnly();

        public void RemoveDirectory(string path, bool keepTracks)
        {
            var removed = Directories.RemoveAll(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (removed <= 0) return;

            if (!keepTracks)
            {
                var tracksToRemove = _library.Tracks.Where(t =>
                    t.FilePath.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var track in tracksToRemove)
                    _library.Tracks.Remove(track);
            }

            _store.Save(_library);
        }

        public async Task ScanDirectory(string path, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"The directory '{path}' does not exist.");
            }
            var files = GetFilesFromDirectory(path, true);
            await ScanFiles(files, progress, cancellationToken);
        }

        public async Task ScanFiles(string[] paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            if (paths is null || paths.Length == 0) return;

            var cts = cancellationToken ?? CancellationToken.None;

            await Task.Run(() => // delegate to background thread for IO-bound work
            {
                // Fingerprinting
                var trackByFingerprint = new Dictionary<(long size, long ticks), Track>(_library.Tracks.Count);
                for (int i = 0; i < _library.Tracks.Count; i++)
                {
                    var t = _library.Tracks[i];
                    trackByFingerprint[(t.FileSize, t.LastWriteTime)] = t;
                }

                var totalFiles = paths.Length;
                var processedFiles = 0;

                progress?.Report(new ScanProgress
                {
                    TotalFiles = totalFiles,
                    FilesProcessed = processedFiles,
                    CurrentFile = string.Empty
                });


                var lastProgressAt = Environment.TickCount64;
                const int progressIntervalMs = 250;
                const int progressEveryNFiles = 100;

                for (int i = 0; i < paths.Length; i++)
                {
                    cancellationToken?.ThrowIfCancellationRequested();
                    string filePath = paths[i];

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        long size = fileInfo.Length;
                        long ticks = fileInfo.LastWriteTimeUtc.Ticks;
                        var key = (size, ticks);

                        if (trackByFingerprint.TryGetValue(key, out var existingTrack)) // file exists in the library
                        {
                            if (!string.Equals(existingTrack.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                                existingTrack.FilePath = filePath; // Update path if it has changed
                        }
                        else
                        {
                            _indexer.IndexFileAsync(filePath);

                            if (_library.Tracks.Count > 0)
                            {
                                var last = _library.Tracks[^1];
                                trackByFingerprint[(last.FileSize, last.LastWriteTime)] = last;
                            }
                        }

                        processedFiles++;

                        bool byCount = (processedFiles % progressEveryNFiles) == 0;
                        bool byTime = (Environment.TickCount64 - lastProgressAt) >= progressIntervalMs;
                        if (byCount || byTime)
                        {
                            progress?.Report(new ScanProgress
                            {
                                TotalFiles = totalFiles,
                                FilesProcessed = processedFiles,
                                CurrentFile = filePath
                            });
                            lastProgressAt = Environment.TickCount64;
                        }
                    }
                    catch
                    {

                    }
                }

                progress?.Report(new ScanProgress
                {
                    TotalFiles = totalFiles,
                    FilesProcessed = processedFiles,
                    CurrentFile = string.Empty
                });
            }, cts);
        }

        #region Internal
        private string[] GetFilesFromDirectory(string path, bool recursive)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            try
            {
                return [.. Directory.GetFiles(path, "*.*", searchOption).Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))];

            }
            catch
            {
                return [];
            }

        }
        #endregion
    }

    public sealed class ScanProgress
    {
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
        public ScanState State { get; set; }
    }

    public enum ScanState
    {
        Fingerprinting,
        Scanning,
        Saving
    }
}
