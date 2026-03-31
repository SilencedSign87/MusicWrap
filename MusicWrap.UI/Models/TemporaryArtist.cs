using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicWrap.UI.Models;

/// <summary>
/// Represents a temporary artist that can be reused across multiple staged tracks.
/// </summary>
public sealed partial class TemporaryArtist : ObservableObject
{
    /// <summary>
    /// Unique temporary ID for this artist session.
    /// </summary>
    public required string Id { get; init; }
    
    [ObservableProperty]
    private string name = string.Empty;
    
    /// <summary>
    /// Optional: if linked to an existing library artist
    /// </summary>
    public string? ExistingArtistId { get; set; }
}
