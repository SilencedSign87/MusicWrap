using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Core.Services.Library.Models
{
    public sealed class LibraryIntegrityReport
    {
        public DateTime GeneratedAt { get; init; }
        public int TotalTracksChecked { get; init; }
        public int TotalDirectoriesChecked { get; init; }
        public List<MissingTrack> MissingTracks { get; init; } = [];
        public List<MovedTrack> MovedTracks { get; init; } = [];
        public List<OrphanedCover> OrphanedCovers { get; init; } = [];
        public List<DuplicateTrack> DuplicateTracks { get; init; } = [];
        public int TotalIssues => MissingTracks.Count + MovedTracks.Count + OrphanedCovers.Count + DuplicateTracks.Count;
        public int AutoFixedCount => MovedTracks.Count(m => m.AutoFixed);
    }
    public sealed class MissingTrack
    {
        public int TrackId { get; init; }
        public required string ExpectedPath { get; init; }
        public string? Title { get; init; }
        public string? ArtistNames { get; init; }
        public string? AlbumName { get; init; }

        // User resolution (set by UI before commit)
        public MissingTrackResolution Resolution { get; set; } = MissingTrackResolution.None;
        public string? LocatedPath { get; set; }
    }
    public enum MissingTrackResolution
    {
        None,
        Remove,
        Locate
    }
    public sealed class MovedTrack
    {
        public int TrackId { get; init; }
        public required string OldPath { get; init; }
        public required string NewPath { get; init; }
        public string? Title { get; init; }
        public bool AutoFixed { get; init; }
    }
    public sealed class OrphanedCover
    {
        public int CoverId { get; init; }
        public required string FileName { get; init; }
    }
    public sealed class DuplicateTrack
    {
        public int TrackId { get; init; }
        public int DuplicateOfTrackId { get; init; }
        public required string FilePath { get; init; }
    }
    public sealed class LibraryVerificationProgress
    {
        public float Progress => TotalTracks > 0 ? (float)ProcessedFiles / TotalTracks * 100f : 0f;
        public int TotalTracks { get; init; }
        public int ProcessedFiles { get; init; }
        public string? CurrentFile { get; init; }
        public VerificationStage Stage { get; init; } = VerificationStage.CheckingFiles;
    }
    public enum VerificationStage
    {
        CheckingFiles,
        CheckingCovers,
        CheckingDuplicates,
        AutoFixing,
        Complete
    }

}
