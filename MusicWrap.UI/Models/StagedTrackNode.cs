using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicWrap.UI.Models;

/// <summary>
/// Represents a track staged for indexing with editable metadata.
/// </summary>
public sealed partial class StagedTrackNode : ObservableObject
{
    public required string ExternalId { get; init; }
    public required int Index { get; init; }
    public string? ArtworkId { get; set; }
    
    [ObservableProperty]
    private string title = string.Empty;
    
    [ObservableProperty]
    private string artist = string.Empty;
    
    [ObservableProperty]
    private string album = string.Empty;
    
    [ObservableProperty]
    private string genre = string.Empty;
    
    [ObservableProperty]
    private int trackNumber;
    
    [ObservableProperty]
    private int discNumber = 1;
    
    [ObservableProperty]
    private int year;
    
    [ObservableProperty]
    private string duration = string.Empty;
    
    [ObservableProperty]
    private string thumbnailUrl = string.Empty;

    [ObservableProperty]
    private string thumbnailHighResUrl = string.Empty;
    
    /// <summary>
    /// Local file path after staging/download.
    /// </summary>
    public string? LocalPath { get; set; }
}
