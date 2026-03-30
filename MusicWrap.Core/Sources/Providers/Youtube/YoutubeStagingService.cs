using System;
using System.Diagnostics;
using System.IO;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.User.Models;
using SixLabors.ImageSharp.ColorSpaces.Companding;
using YoutubeExplode;

namespace MusicWrap.Core.Sources.Providers.Youtube;

public class YoutubeStagingService : IYoutubeStagingService
{
    private readonly YoutubeClient _youtube = new();
    private readonly string _cacheDir;
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _ready = new();
    private readonly UserSettings _settings;
    public YoutubeStagingService(UserSettings userSettings)
    {
        _cacheDir = Path.Combine(MusicWrapDirectories.CacheDirectory, "YoutubeAudio");
        _settings = userSettings;
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<string?> GetPlayableFileAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return null;

        lock (_lock)
        {
            if (_ready.TryGetValue(videoId, out var existing) && File.Exists(existing))
                return existing;
        }

        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken).ConfigureAwait(false);

        var audio = manifest.GetAudioOnlyStreams()
               .OrderByDescending(s => s.Bitrate)
               .FirstOrDefault();

        if (audio == null) return null;

        // download stream to a temp file
        string inputExt = audio.Container.Name;
        string inputPath = Path.Combine(_cacheDir, $"{videoId}.src.{inputExt}");
        string outputPath = Path.Combine(_cacheDir, $"{videoId}.flac");
        if (!File.Exists(outputPath))
        {
            await using (var src = await _youtube.Videos.Streams.GetAsync(audio, cancellationToken).ConfigureAwait(false))
            await using (var dst = File.Create(inputPath))
            {
                await src.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
            }

            // transcode to flac using ffmpeg
            bool ok = await RunFfmpegAsync(inputPath, outputPath, cancellationToken).ConfigureAwait(false);

            try { File.Delete(inputPath); } catch { /* best effort cleanup */ }

            if (!ok || !File.Exists(outputPath))
                return null;
        }
        lock (_lock)
        {
            _ready[videoId] = outputPath;
        }
        return outputPath;
    }

    private async Task<bool> RunFfmpegAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        string ffmpegExe = ResolveFfmpegPath();
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            Arguments = $"-y -hide_banner -loglevel error -i \"{inputPath}\" -vn -c:a flac \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        if (!process.Start())
        {
            return false;
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode == 0;
    }
    private string ResolveFfmpegPath()
    {
        if (_settings.UseCustomFfmpegPath &&
        !string.IsNullOrWhiteSpace(_settings.CustomFfmpegPath) &&
        File.Exists(_settings.CustomFfmpegPath))
        {
            return _settings.CustomFfmpegPath;
        }

        // fallback: PATH
        return "ffmpeg";
    }
}
