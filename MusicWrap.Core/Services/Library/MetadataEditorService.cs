using Microsoft.Extensions.Logging;
using MusicWrap.Core.Saving;
using MusicWrap.Data.Infrastructure.Saving;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Library
{
    public class MetadataEditorService
    {
        private readonly ILibraryService _library;
        private readonly ISaveCoordinator _saveCoordinator;
        private readonly ILogger<MetadataEditorService> _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public MetadataEditorService(ILibraryService library, ISaveCoordinator saveCoordinator, ILogger<MetadataEditorService> logger)
        {
            _library = library;
            _saveCoordinator = saveCoordinator;
            _logger = logger;
        }
        public async Task<bool> EditTagAsync(int trackId, Action<TagLib.Tag> applyChanges, CancellationToken ct = default)
        {
            var track = _library.GetTrackById(trackId);
            if (track is null || track.Id == 0 || string.IsNullOrWhiteSpace(track.Path)) return false;

            await _writeLock.WaitAsync();
            try
            {
                return await Task.Run(() => EditTagsCore(track, applyChanges));
            }
            finally
            {
                _writeLock.Release();
            }
        }
        private bool EditTagsCore(Track track, Action<TagLib.Tag> applyChanges)
        {
            string? tempPath = null;
            try
            {
                if (!System.IO.File.Exists(track.Path)) return false;

                byte[] fileData = System.IO.File.ReadAllBytes(track.Path);

                //using var ms = new MemoryStream(fileData, writable: true);
                // modify the tag in memory
                // expandable memory stream
                using var ms = new MemoryStream();
                ms.Write(fileData, 0, fileData.Length);
                ms.Position = 0;

                using (var tagFile = TagLib.File.Create(new StreamFileAbstraction(track.Path, ms)))
                {
                    applyChanges(tagFile.Tag);
                    tagFile.Save();
                }

                // write to temp
                tempPath = WriteToTempFile(ms, track.Path);
                System.IO.File.Replace(tempPath, track.Path, null);
                tempPath = null;

                // refresh metadata
                RefreshTrackFromStream(track, ms);

                _saveCoordinator.Enqueue(SaveKind.Library);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to edit tags for file {Path}", track.Path);
                return false;
            }
            finally
            {
                if (tempPath != null)
                {
                    try { System.IO.File.Delete(tempPath); } catch { /* best effort */ }
                }
            }
        }

        private void RefreshTrackFromStream(Track track, MemoryStream ms)
        {
            try
            {
                ms.Position = 0;
                using var tagFile = TagLib.File.Create(new StreamFileAbstraction(track.Path, ms));
                track.Title = tagFile.Tag.Title
                    ?? System.IO.Path.GetFileNameWithoutExtension(track.Path);
                track.Duration = (int)tagFile.Properties.Duration.TotalSeconds;
                track.SamplingRate = tagFile.Properties.AudioSampleRate;
                track.Bitrate = tagFile.Properties.AudioBitrate;
                track.Channels = tagFile.Properties.AudioChannels;
                track.BitDeph = tagFile.Properties.BitsPerSample;
                track.Disk = (int)tagFile.Tag.Disc;
                track.TrackNumber = (int)tagFile.Tag.Track;
                var fileInfo = new FileInfo(track.Path);
                track.FileSize = fileInfo.Length;
                track.LastWriteTime = File.GetLastWriteTimeUtc(track.Path).Ticks;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh metadata from stream for {Path}", track.Path);
                try
                {
                    var fileInfo = new FileInfo(track.Path);
                    track.FileSize = fileInfo.Length;
                    track.LastWriteTime = File.GetLastWriteTimeUtc(track.Path).Ticks;
                }
                catch { /* best effort */ }
            }
        }
        private static string WriteToTempFile(MemoryStream ms, string originalPath)
        {
            string dir = System.IO.Path.GetDirectoryName(originalPath)!;
            string tempPath = System.IO.Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
            ms.Position = 0;
            using (var tempFs = System.IO.File.Create(tempPath))
            {
                ms.CopyTo(tempFs);
            }
            return tempPath;
        }
        private sealed class StreamFileAbstraction : TagLib.File.IFileAbstraction
        {
            private readonly MemoryStream _stream;
            public string Name { get; }
            public Stream ReadStream => _stream;
            public Stream WriteStream => _stream;
            public StreamFileAbstraction(string name, MemoryStream stream)
            {
                Name = name;
                _stream = stream;
            }
            public void CloseStream(Stream stream) { }
        }
    }
}
