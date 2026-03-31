using System;

namespace MusicWrap.Core.Sources.Contracts;

public interface IYoutubeStagingService
{
    Task<string?> GetPlayableFileAsync(string videoId, CancellationToken cancellationToken = default);
}

public sealed class YoutubeStagingException : Exception
{
    public bool IsFfmpegConfigurationError { get; }

    public YoutubeStagingException(string message, bool isFfmpegConfigurationError = false, Exception? innerException = null)
        : base(message, innerException)
    {
        IsFfmpegConfigurationError = isFfmpegConfigurationError;
    }
}
