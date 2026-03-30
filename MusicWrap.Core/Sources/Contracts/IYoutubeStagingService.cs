using System;

namespace MusicWrap.Core.Sources.Contracts;

public interface IYoutubeStagingService
{
    Task<string?> GetPlayableFileAsync(string videoId, CancellationToken cancellationToken = default);
}
