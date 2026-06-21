using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MusicWrap.Data.Helpers
{
    public static class FormatHelpers
    {
        public static string FormatTrackNumber(int trackNumber, int totalTracks)
            => AppStringPool.Intern($"{trackNumber}/{totalTracks}") ?? "";
        public static string FormatTrackNumber(int trackNumber)
            => AppStringPool.Intern($"Track {trackNumber}") ?? "";
        
        public static string FormatDiscNumber(int discNumber, int totalDiscs)
            => AppStringPool.Intern($"{discNumber}/{totalDiscs}") ?? "";
        
        public static string FormatDiscNumber(int discNumber)
            => AppStringPool.Intern($"Disc {discNumber}") ?? "";
        public static string FormatDuration(TimeSpan duration)
        {
            var result = duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
            return AppStringPool.Intern(result) ?? result;
        }
        public static string FormatDuration(int seconds)
            => FormatDuration(TimeSpan.FromSeconds(seconds));
        
        public static string FormatBitrate(int bitrate) // already in kbps
            => AppStringPool.Intern($"{bitrate} kbps") ?? "";
        public static string FormatSampleRate(int sampleRate)
        {
            if (sampleRate <= 0) return "";
            var result = sampleRate >= 1000
                ? $"{sampleRate / 1000.0:F1} kHz"
                : $"{sampleRate} Hz";
            return AppStringPool.Intern(result) ?? result;
        }
        public static string FormatBitDepth(int bitDepth)
        {
            return AppStringPool.Intern($"{bitDepth} bit") ?? "";
        }
        public static string FormatChannels(int channels)
        {
            return AppStringPool.Intern($"{channels} channel{(channels > 1 ? "s" : "")}") ?? "";
        }
        public static string FormatFileSize(long fileSize)
        {
            if (fileSize >= 1_000_000_000)
                return AppStringPool.Intern($"{fileSize / 1_000_000_000.0:F2} GB") ?? "";
            else if (fileSize >= 1_000_000)
                return AppStringPool.Intern($"{fileSize / 1_000_000.0:F2} MB") ?? "";
            else if (fileSize >= 1_000)
                return AppStringPool.Intern($"{fileSize / 1_000.0:F2} KB") ?? "";
            else
                return AppStringPool.Intern($"{fileSize} B") ?? "";
        }
        public static string FormatDateTime(DateTime dateTime)
        {
            return AppStringPool.Intern(dateTime.ToString("yyyy-MM-dd HH:mm:ss")) ?? "";
        }
        public static string FormatFileExtension(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return "";
            var ext = Path.GetExtension(filePath)?.TrimStart('.').ToUpperInvariant();
            return AppStringPool.Intern(ext) ?? "";
        }
        public static string FormatTrackWithDisc(int trackNumber, int discNumber)
        {
            if (trackNumber <= 0) return "";
            var track = FormatTrackNumber(trackNumber);
            if (discNumber > 0)
                return AppStringPool.Intern($"{track} ({FormatDiscNumber(discNumber).ToLower()})") ?? track;
            return track;
        }
    }
}
