using System;

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

        public int AutoFixedCount => MovedTracks.Count(m => m.AutoFixed);

        public int PendingUserReview =>
            MissingTracks.Count +
            OrphanedCovers.Count(c => c.Resolution == CoverResolution.None) +
            DuplicateTracks.Count(d => d.Resolution == DuplicateResolution.None);

        public int TotalIssues => PendingUserReview + AutoFixedCount;
    }
    public sealed class MissingTrack : TrackIssueInfo
    {
        public int TrackId { get; init; }
        public required string ExpectedPath { get; init; }

        public MissingTrackResolution Resolution { get; set; } = MissingTrackResolution.None;
        public string? LocatedPath { get; set; }
    }
    public sealed class MovedTrack : TrackIssueInfo
    {
        public int TrackId { get; init; }
        public required string OldPath { get; init; }
        public required string NewPath { get; init; }
        public bool AutoFixed { get; init; } // true = path updated
    }
    public sealed class OrphanedCover // cover with no reference
    {
        public int CoverId { get; init; }
        public required string FileName { get; init; }
        public CoverResolution Resolution { get; set; } = CoverResolution.Remove;
    }
    public sealed class DuplicateTrack
    {
        public int TrackIdToRemove { get; init; }
        public int KeepTrackId { get; init; }
        public required string FilePath { get; init; }
        public DuplicateResolution Resolution { get; set; } = DuplicateResolution.RemoveDuplicate;
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
    public enum MissingTrackResolution
    {
        None,    // No decision made yet
        Remove,  // Remove track from library
        Locate,  // New path provided
        Ignore   // Dont do anything
    }
    public enum CoverResolution
    {
        None,
        Remove,
        Keep
    }
    public enum DuplicateResolution
    {
        None,
        RemoveDuplicate,
        KeepBoth
    }

    public abstract class TrackIssueInfo
    {
        public required string Title { get; init; }
        public string? ArtistNames { get; init; }
        public string? AlbumName { get; init; }
    }
}
