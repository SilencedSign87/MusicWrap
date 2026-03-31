using MusicWrap.Data.Library;
using MusicWrap.Data.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicWrap.Core.Metadata
{
    /// <summary>
    /// Implementation of metadata autocomplete service
    /// Provides suggestions from the music library
    /// </summary>
    public class MetadataAutocompleteService : IMetadataAutocompleteService
    {
        private readonly ILibraryRepository _libraryRepository;

        public MetadataAutocompleteService(ILibraryRepository libraryRepository)
        {
            _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        }

        public IReadOnlyList<string> GetSuggestions(MetadataType metadataType, string searchTerm, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return GetAllValues(metadataType).Take(limit).ToList();

            var allValues = GetAllValues(metadataType);
            var searchLower = searchTerm.ToLowerInvariant();

            // Sort by relevance:
            // 1. Exact match (case-insensitive)
            // 2. Starts with search term
            // 3. Contains search term (substring)
            var suggestions = allValues
                .OrderByDescending(v => v.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(v => v.StartsWith(searchLower, StringComparison.OrdinalIgnoreCase))
                .ThenBy(v => v.IndexOf(searchLower, StringComparison.OrdinalIgnoreCase))
                .Where(v => v.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            return suggestions;
        }

        public IReadOnlyList<string> GetAllValues(MetadataType metadataType)
        {
            try
            {
                var library = _libraryRepository.Load();

                return metadataType switch
                {
                    MetadataType.ArtistName =>
                        library.Artists
                            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                            .Select(a => a.Name)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(n => n)
                            .ToList(),

                    MetadataType.AlbumTitle =>
                        library.Albums
                            .Where(a => !string.IsNullOrWhiteSpace(a.Title))
                            .Select(a => a.Title)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(n => n)
                            .ToList(),

                    MetadataType.GenreName =>
                        library.Genres
                            .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                            .Select(g => g.Name)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(n => n)
                            .ToList(),

                    _ => []
                };
            }
            catch
            {
                // If library loading fails, return empty list
                return [];
            }
        }
    }
}
