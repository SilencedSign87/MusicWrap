using Microsoft.Extensions.Logging;
using MusicWrap.Core.Services.Library.Models;
using MusicWrap.Data.Library.Models;

namespace MusicWrap.Core.Services.Library
{
    public interface ILibraryIntegrityService
    {
        Task<LibraryIntegrityReport> VerifyAsync(IProgress<LibraryVerificationProgress>? progress = null, CancellationToken cancellationToken = default);
    }

    public class LibraryIntegrityService : ILibraryIntegrityService
    {
        private readonly MusicLibrary _library;
        private readonly ILogger _logger;

        public LibraryIntegrityService(MusicLibrary library, ILogger<LibraryIntegrityService> logger)
        {
            _library = library;
            _logger = logger;
        }

        public Task<LibraryIntegrityReport> VerifyAsync(IProgress<LibraryVerificationProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
