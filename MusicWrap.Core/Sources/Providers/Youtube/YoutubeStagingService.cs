using Microsoft.Extensions.Logging;
using MusicWrap.Core.Sources.Contracts;
using MusicWrap.Data.Infrastructure;
using MusicWrap.Data.User.Models;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using YoutubeExplode;

namespace MusicWrap.Core.Sources.Providers.Youtube;

public class YoutubeStagingService : IYoutubeStagingService
{
    private readonly YoutubeClient _youtube = new();
    private readonly string _cacheDir;
    private readonly object _lock = new();
    private readonly Dictionary<string, string> _ready = new();
    private readonly UserSettings _settings;
    private readonly ILogger<YoutubeStagingService> _logger;

    public YoutubeStagingService(UserSettings userSettings, ILogger<YoutubeStagingService> logger)
    {
        _cacheDir = Path.Combine(MusicWrapDirectories.CacheDirectory, "YoutubeAudio");
        _settings = userSettings;
        _logger = logger;
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<string?> GetPlayableFileAsync(string videoId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return null;
        var outputOptions = ResolveOutputOptions();
        string outputExt = outputOptions.OutputExtension;

        string outputFile = Path.Combine(_cacheDir, $"{videoId}.{outputExt}");

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
            {
                string existingExt = Path.GetExtension(existing).TrimStart('.').ToLowerInvariant();
                if (existingExt == outputExt)
                {
                    return existing;
                }

                _ready.Remove(videoId);
            }
        }

        var manifest = await _youtube.Videos.Streams.GetManifestAsync(videoId, cancellationToken).ConfigureAwait(false);

        var audio = manifest.GetAudioOnlyStreams()
               .OrderByDescending(s => s.Bitrate)
               .FirstOrDefault();

        if (audio == null) return null;

        // download stream to a temp file
        string inputExt = audio.Container.Name; // default webm
        string inputPath = Path.Combine(_cacheDir, $"{videoId}.src.{inputExt}");
        string outputPath = Path.Combine(_cacheDir, $"{videoId}.{outputExt}");
        if (!File.Exists(outputPath))
        {
            try
            {
                await using (var src = await _youtube.Videos.Streams.GetAsync(audio, cancellationToken).ConfigureAwait(false))
                await using (var dst = File.Create(inputPath))
                {
                    await src.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
                }

                await RunFfmpegAsync(inputPath, outputPath, outputOptions, cancellationToken).ConfigureAwait(false);

                try { File.Delete(inputPath); } catch { /* best effort cleanup */ }

                if (!File.Exists(outputPath))
                {
                    InvalidateCachedFile(videoId);
                    return null;
                }
            }
            catch
            {
                InvalidateCachedFile(videoId);
                throw;
            }
        }

        lock (_lock)
        {
            _ready[videoId] = outputPath;
        }

        return outputPath;
    }

    public void InvalidateCachedFile(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            return;
        }

        lock (_lock)
        {
            _ready.Remove(videoId);
        }

        try
        {
            if (!Directory.Exists(_cacheDir))
            {
                return;
            }

            foreach (var path in Directory.EnumerateFiles(_cacheDir, $"{videoId}.*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to delete cached Youtube file {FilePath} for video {VideoId}", path, videoId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to enumerate cache files for Youtube video {VideoId}", videoId);
        }
    }

    private async Task RunFfmpegAsync(string inputPath, string outputPath, FfmpegOutputOptions outputOptions, CancellationToken cancellationToken)
    {
        string preferredAudioExtension = outputOptions.OutputExtension;

        string ffmpegExe = ResolveFfmpegPath();
        string formatArg = string.IsNullOrWhiteSpace(outputOptions.FormatName)
            ? string.Empty
            : $" -f {outputOptions.FormatName}";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            Arguments = $"-y -hide_banner -loglevel error -i \"{inputPath}\" -vn -c:a {outputOptions.AudioCodec}{formatArg} \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        _logger.LogInformation("Converting Youtube audio to {Format} using codec {Codec}", preferredAudioExtension, outputOptions.AudioCodec);

        using var process = new Process { StartInfo = psi };

        try
        {
            if (!process.Start())
            {
                throw new YoutubeStagingException("Can not initialize ffmpeg.", isFfmpegConfigurationError: true);
            }
        }
        catch (Win32Exception ex)
        {
            throw new YoutubeStagingException(
                "ffmpeg is not configured or not found. Configure the ffmpeg path in Settings > Youtube.",
                isFfmpegConfigurationError: true,
                innerException: ex);
        }

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            string stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            string details = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new YoutubeStagingException($"ffmpeg failed to convert audio to {preferredAudioExtension}. {details}".Trim());

        }
    }

    private FfmpegOutputOptions ResolveOutputOptions()
    {
        return _settings.YoutubeSettings.PreferredAudioFormatForYoutube switch
        {
            SuportedFFMpegAudioFormat.webm => new FfmpegOutputOptions("webm", "libopus", "webm"),
            SuportedFFMpegAudioFormat.mp3 => new FfmpegOutputOptions("mp3", "libmp3lame"),
            SuportedFFMpegAudioFormat.aac => new FfmpegOutputOptions("aac", "aac", "adts"),
            SuportedFFMpegAudioFormat.flac => new FfmpegOutputOptions("flac", "flac"),
            SuportedFFMpegAudioFormat.wav => new FfmpegOutputOptions("wav", "pcm_s16le"),
            SuportedFFMpegAudioFormat.opus => new FfmpegOutputOptions("opus", "libopus", "opus"),
            SuportedFFMpegAudioFormat.vorbis => new FfmpegOutputOptions("ogg", "libvorbis", "ogg"),
            SuportedFFMpegAudioFormat.alac => new FfmpegOutputOptions("m4a", "alac"),
            SuportedFFMpegAudioFormat.ac3 => new FfmpegOutputOptions("ac3", "ac3"),
            SuportedFFMpegAudioFormat.eac3 => new FfmpegOutputOptions("eac3", "eac3"),
            _ => new FfmpegOutputOptions("mp3", "libmp3lame")
        };
    }

    private string ResolveFfmpegPath()
    {
        if (_settings.FFMpegSettings.UseCustomFfmpegPath)
        {
            if (string.IsNullOrWhiteSpace(_settings.FFMpegSettings.CustomFfmpegPath))
            {
                throw new YoutubeStagingException(
                    "No hay ruta de ffmpeg configurada. Configura ffmpeg en Settings > Youtube.",
                    isFfmpegConfigurationError: true);
            }

            if (!File.Exists(_settings.FFMpegSettings.CustomFfmpegPath))
            {
                throw new YoutubeStagingException(
                    $"La ruta configurada de ffmpeg no existe: {_settings.FFMpegSettings.CustomFfmpegPath}",
                    isFfmpegConfigurationError: true);
            }

            return _settings.FFMpegSettings.CustomFfmpegPath;
        }

        // fallback: PATH
        return "ffmpeg";
    }
    private readonly record struct FfmpegOutputOptions(string OutputExtension, string AudioCodec, string? FormatName = null);
}
