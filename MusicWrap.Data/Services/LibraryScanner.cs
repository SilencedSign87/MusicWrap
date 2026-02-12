using MusicWrap.Data.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.Data.Services
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
        private readonly ILibraryStore _store;
        private readonly ILibraryIndexer _indexer;

        private static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".aac", ".ogg", ".opus", ".m4a"];

        private List<ScanDirectory> Directories
        {
            get
            {
                _library.Directories ??= new List<ScanDirectory>();
                return _library.Directories;
            }
        }

        public LibraryScanner(MusicLibrary library, ILibraryStore store, ILibraryIndexer indexer)
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
                    t.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();
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

            var totalFiles = paths.Length;
            var processedFiles = 0;

            progress?.Report(new ScanProgress
            {
                TotalFiles = totalFiles,
                FilesProcessed = processedFiles,
                CurrentFile = string.Empty
            });

            foreach (var filePath in paths)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                try
                {
                    progress?.Report(new ScanProgress
                    {
                        TotalFiles = totalFiles,
                        FilesProcessed = processedFiles,
                        CurrentFile = filePath
                    });
                    await ProcessAudioFile(filePath);
                    processedFiles++;
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
        }

        #region Internal
        private string[] GetFilesFromDirectory(string path, bool recursive)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            try
            {
                return [.. Directory.GetFiles(path, "*.*", searchOption).Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))];

            }
            catch
            {
                return [];
            }

        }

        private Task ProcessAudioFile(string filepath)
        {
            return Task.Run(() =>
            {
                _indexer.IndexFileAsync(filepath);
            });
        }
        #endregion
    }
}
