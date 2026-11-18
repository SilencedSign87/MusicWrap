using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicWrap.Core.Services.Lyrics;
using MusicWrap.Data.Library.Models;
using MusicWrap.UI.Features.Metadata.Services;
using MusicWrap.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;

namespace MusicWrap.UI.Features.Metadata.Viewmodels
{
    public partial class MetadataLyricsEditorViewmodel : ObservableObject, IMetadataEditorTabViewmodel
    {
        private readonly LyricsService _lyricsService;
        private readonly MetadataEditorWorkspace _workspace;

        [ObservableProperty]
        public partial MusicWrap.Data.Library.Models.Lyrics? DefaultLyrics { get; set; }

        [ObservableProperty]
        public partial bool HasMultipleDefaultLyrics { get; set; } = false;

        [ObservableProperty]
        public partial bool HasMultipleVariations { get; set; } = false;

        public ObservableCollection<MusicWrap.Data.Library.Models.Lyrics> LyricsVariants { get; } = [];



        public MetadataLyricsEditorViewmodel(LyricsService lyricsService, MetadataEditorWorkspace workspace)
        {
            _lyricsService = lyricsService;
            _workspace = workspace;
        }

        public void Load()
        {
            DefaultLyrics = null;
            LyricsVariants.Clear();
            HasMultipleDefaultLyrics = false;
            HasMultipleVariations = false;

            var DefaultLyricsSet = new HashSet<MusicWrap.Data.Library.Models.Lyrics>();
            var VariationsByTackId = new Dictionary<int, MusicWrap.Data.Library.Models.Lyrics>();


            foreach (var trackId in _workspace.TrackIds)
            {
                var trackLyrics = _lyricsService.LoadLyricsByTrackId(trackId);

                var defaultLyrics = trackLyrics.FirstOrDefault(x => x.Original);

                if (defaultLyrics is not null)
                {
                    DefaultLyricsSet.Add(defaultLyrics);
                }

                var variations = trackLyrics.Where(x => !x.Original).ToList();

                if (variations.Count > 0)
                {
                    foreach (var variation in variations)
                    {
                        VariationsByTackId.TryAdd(variation.Id, variation);
                    }
                }

            }

            // TODO: Better way of detecting multiple variations per track.
            if (DefaultLyricsSet.Count > 1)
            {
                HasMultipleDefaultLyrics = true;
            }
            else
            {
                DefaultLyrics = DefaultLyricsSet.FirstOrDefault();
            }
        }

        #region Relay Commands

        #endregion
    }
}
