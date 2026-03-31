using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicWrap.UI.Models;

/// <summary>
/// Represents a temporary album that can be reused across multiple staged tracks.
/// </summary>
public sealed partial class TemporaryAlbum : ObservableObject
{
    /// <summary>
    /// Unique temporary ID for this album session.
    /// </summary>
    public required string Id { get; init; }
    
    [ObservableProperty]
    private string name = string.Empty;
    
    [ObservableProperty]
    private string artist = string.Empty;
    
    [ObservableProperty]
    private int year;
    
    [ObservableProperty]
    private string genre = string.Empty;
    
    /// <summary>
    /// Optional: if linked to an existing library album
    /// </summary>
    public string? ExistingAlbumId { get; set; }
}
