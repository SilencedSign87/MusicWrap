namespace MusicWrap.Core.Metadata
{
    /// <summary>
    /// Metadata type for autocomplete suggestions
    /// </summary>
    public enum MetadataType
    {
        ArtistName,
        AlbumTitle,
        GenreName
    }

    /// <summary>
    /// Service for providing metadata autocomplete suggestions from the music library
    /// </summary>
    public interface IMetadataAutocompleteService
    {
        /// <summary>
        /// Get autocomplete suggestions for a given metadata type and search term
        /// </summary>
        /// <param name="metadataType">Type of metadata to search for</param>
        /// <param name="searchTerm">Text to search for (case-insensitive partial match)</param>
        /// <param name="limit">Maximum number of suggestions to return</param>
        /// <returns>List of matching suggestions, sorted by relevance</returns>
        IReadOnlyList<string> GetSuggestions(MetadataType metadataType, string searchTerm, int limit = 20);

        /// <summary>
        /// Get all unique values for a metadata type
        /// </summary>
        IReadOnlyList<string> GetAllValues(MetadataType metadataType);
    }
}
