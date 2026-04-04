using System;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
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

        string outputFile = Path.Combine(_cacheDir, $"{videoId}.flac");

        if (File.Exists(outputFile))
        {
             lock (_lock)
             {
                 _ready[videoId] = outputFile;
             }
             return outputFile;
        }

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
            await RunFfmpegAsync(inputPath, outputPath, cancellationToken).ConfigureAwait(false);

            try { File.Delete(inputPath); } catch { /* best effort cleanup */ }

            if (!File.Exists(outputPath))
                return null;
        }
        lock (_lock)
        {
            _ready[videoId] = outputPath;
        }
        return outputPath;
    }

    private async Task RunFfmpegAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
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

        try
        {
            if (!process.Start())
            {
                throw new YoutubeStagingException("No se pudo iniciar ffmpeg.", isFfmpegConfigurationError: true);
            }
        }
        catch (Win32Exception ex)
        {
            throw new YoutubeStagingException(
                "ffmpeg no esta configurado o no se encontro. Configura la ruta de ffmpeg en Settings > Youtube.",
                isFfmpegConfigurationError: true,
                innerException: ex);
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new YoutubeStagingException($"ffmpeg fallo al convertir audio a FLAC. {details}".Trim());
        }
    }

    private string ResolveFfmpegPath()
    {
        if (_settings.UseCustomFfmpegPath)
        {
            if (string.IsNullOrWhiteSpace(_settings.CustomFfmpegPath))
            {
                throw new YoutubeStagingException(
                    "No hay ruta de ffmpeg configurada. Configura ffmpeg en Settings > Youtube.",
                    isFfmpegConfigurationError: true);
            }

            if (!File.Exists(_settings.CustomFfmpegPath))
            {
                throw new YoutubeStagingException(
                    $"La ruta configurada de ffmpeg no existe: {_settings.CustomFfmpegPath}",
                    isFfmpegConfigurationError: true);
            }

            return _settings.CustomFfmpegPath;
        }

        // fallback: PATH
        return "ffmpeg";
    }
}
