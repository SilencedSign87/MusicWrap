using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MusicWrap.Core.Messages;
using MusicWrap.Core.Saving;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Channels;

namespace MusicWrap.Core.Services.Library
{
    public interface ILibraryScanner
    {
        Task ScanAllDirectories(IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        Task ScanSpecificDirectories(string[] paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        Task ScanDirectory(string path, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        Task ScanFiles(IEnumerable<string> paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken);
        void AddDirectory(string path, bool recursive);
        void RemoveDirectory(string path, bool keepTracks);
        IReadOnlyList<RawMetadata> GetRawMetadata(string path);
        IReadOnlyList<ScanDirectory> GetDirectories();
    }
    public class LibraryScanner : ILibraryScanner
    {
        private readonly IMessenger _messenger;
        private readonly MusicLibrary _library;
        private readonly ILibraryIndexer _indexer;
        private readonly ILogger _logger;
        private readonly ISaveCoordinator _saveCoordinator;

        private List<ScanDirectory> Directories
        {
            get
            {
                _library.Directories ??= new List<ScanDirectory>();
                return _library.Directories;
            }
        }

        public LibraryScanner(MusicLibrary library, ILibraryIndexer indexer, ILogger<LibraryScanner> logger, ISaveCoordinator saveCoordinator, IMessenger messenger)
        {
            _library = library;
            _indexer = indexer;
            _logger = logger;
            _saveCoordinator = saveCoordinator;
            _messenger = messenger;
        }
        public async Task ScanAllDirectories(IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            var cts = cancellationToken ?? CancellationToken.None;

            var allPaths = Directories
                .Where(d => !string.IsNullOrWhiteSpace(d.Path))
                .SelectMany(d => GetFilesFromDirectory(d.Path, d.Recursive));

            await ScanFiles(allPaths, progress, cancellationToken).ConfigureAwait(false);

            _saveCoordinator.Enqueue(SaveKind.Library);
        }

        public async Task ScanSpecificDirectories(string[] paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            var cts = cancellationToken ?? CancellationToken.None;

            var selected = new HashSet<string>(
                paths.Where(p => !string.IsNullOrWhiteSpace(p)),
                StringComparer.OrdinalIgnoreCase);

            var allPaths = Directories
                .Where(d => selected.Contains(d.Path))
                .SelectMany(d => GetFilesFromDirectory(d.Path, d.Recursive));

            await ScanFiles(allPaths, progress, cancellationToken).ConfigureAwait(false);

            _saveCoordinator.Enqueue(SaveKind.Library);
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

            _saveCoordinator.Enqueue(SaveKind.Library);
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

                if (tracksToRemove.Count > 0)
                {
                    _messenger.Send(new LibraryChangedMessage(LibraryChangeType.FullReload));
                }
            }

            _saveCoordinator.Enqueue(SaveKind.Library);
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

        public async Task ScanFiles(IEnumerable<string> paths, IProgress<ScanProgress>? progress, CancellationToken? cancellationToken)
        {
            if (paths is null) return;

            var cts = cancellationToken ?? CancellationToken.None;

            const int consumers = 4;
            var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(consumers)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            });

            var totalFiles = paths.Count();
            var processedFiles = 0;
            var skippedFiles = 0;
            var errorSummary = new ConcurrentDictionary<string, int>();
            var lastProgressAt = Environment.TickCount64;
            const int progressIntervalMs = 250;
            const int progressEveryNFiles = 100;

            progress?.Report(new ScanProgress
            {
                TotalFiles = 0,
                FilesProcessed = 0,
                CurrentFile = string.Empty
            });

            async Task ProduceAsync()
            {
                try
                {
                    foreach (var path in paths)
                    {
                        cts.ThrowIfCancellationRequested();
                        //Interlocked.Increment(ref totalFiles);
                        await channel.Writer.WriteAsync(path, cts).ConfigureAwait(false);
                    }
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }

            async Task ConsumeAsync(int id)
            {
                await foreach (var filepath in channel.Reader.ReadAllAsync(cts).ConfigureAwait(false))
                {
                    try
                    {
                        await _indexer.IndexFileAsync(filepath, cts).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        throw; // propagate cancellation
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref skippedFiles);
                        var key = $"{ex.GetType().Name}: {ex.Message.Split('\n')[0]}";
                        errorSummary.AddOrUpdate(key, 1, (_, current) => current + 1);

                        _logger.LogWarning(ex, "Failed to index file '{FilePath}'", filepath);
                    }

                    var current = Interlocked.Increment(ref processedFiles);

                    if (id == 0)
                    {
                        bool byCount = (current % progressEveryNFiles) == 0;
                        bool byTime = (Environment.TickCount64 - lastProgressAt) >= progressIntervalMs;
                        if (byCount || byTime)
                        {
                            lastProgressAt = Environment.TickCount64;
                            var total = Volatile.Read(ref totalFiles);
                            progress?.Report(new ScanProgress
                            {
                                TotalFiles = total,
                                FilesProcessed = (int)current,
                                CurrentFile = filepath,
                                State = ScanState.Scanning
                            });
                        }
                    }
                }
            }

            // orquestator

            var producer = Task.Run(ProduceAsync, cts);
            var consumerTasks = Enumerable.Range(0, consumers)
                .Select(i => ConsumeAsync(i))
                .ToArray();

            await Task.WhenAll(consumerTasks.Prepend(producer)).ConfigureAwait(false);

            var finalTotal = Volatile.Read(ref totalFiles);
            var finalProcessed = Volatile.Read(ref processedFiles);
            var finalSkipped = Volatile.Read(ref skippedFiles);

            progress?.Report(new ScanProgress
            {
                TotalFiles = finalTotal,
                FilesProcessed = finalProcessed,
                CurrentFile = string.Empty,
                State = ScanState.Saving
            });

            _logger.LogInformation(
                message: "Scan complete. Processed: {Processed}/{Total}, Skipped: {Skipped}",
                finalProcessed, finalTotal, finalSkipped
                );

            if (!errorSummary.IsEmpty)
            {
                _logger.LogInformation("Error summary:");
                foreach (var kvp in errorSummary.OrderByDescending(kv => kv.Value))
                {
                    _logger.LogInformation("  {Count,6}x  {Error}", kvp.Value, kvp.Key);
                }
            }

            if (processedFiles > 0)
            {
                _messenger.Send(new LibraryChangedMessage(LibraryChangeType.FullReload));
                _saveCoordinator.Enqueue(SaveKind.Library);
            }

        }

        public IReadOnlyList<RawMetadata> GetRawMetadata(string path)
        {
            if (!File.Exists(path))
                return [];

            using var tagfile = TagLib.File.Create(path);
            return [];

        }

        #region Internal
        private static IEnumerable<string> GetFilesFromDirectory(string path, bool recursive)
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true,
            };

            return Directory.EnumerateFiles(path, "*", options)
                            .Where(f => MusicWrapConstants.SupportedExtensions.Contains(Path.GetExtension(f)));
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

    public record RawMetadata
    {
        public required string Group { get; set; }
        public required string Key { get; set; }
        public required string Value { get; set; }
    }
}
