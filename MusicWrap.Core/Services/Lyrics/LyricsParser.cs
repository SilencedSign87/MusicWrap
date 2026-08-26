using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TagLib.Id3v2;

namespace MusicWrap.Core.Services.Lyrics
{
    public static class LyricsParser
    {
        private static readonly Regex TimeTagRegex = new(@"\[(\d{1,3}):(\d{2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
        private static readonly Regex OffsetRegex = new(@"\[offset:\s*([+-]?\d+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly HashSet<string> IgnoredHeaders = new(StringComparer.OrdinalIgnoreCase) { "ti", "ar", "al", "by", "ve", "length" };

        public static ParsedLyrics Parse(string? raw, LyricsSource source = LyricsSource.Embedded)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ParsedLyrics.Empty;
            raw = raw.Trim();
            int offsetMs = 0;
            var mOff = OffsetRegex.Match(raw);
            if (mOff.Success) int.TryParse(mOff.Groups[1].Value, out offsetMs);

            var lines = new List<LyricLine>();
            bool anySync = false;

            foreach (var rawLine in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();

                if (line.Length == 0) continue;

                if (line.StartsWith("[") && line.Contains(':') && !TimeTagRegex.IsMatch(line))
                {
                    var header = line.Trim('[', ']').Split(':')[0];
                    if (IgnoredHeaders.Contains(header) || line.StartsWith("[offset:", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var matches = TimeTagRegex.Matches(line);
                if (matches.Count == 0) continue;

                var text = TimeTagRegex.Replace(line, "").Trim();
                foreach (Match m in matches)
                {
                    if (!int.TryParse(m.Groups[1].Value, out var min)) continue;
                    if (!int.TryParse(m.Groups[2].Value, out var sec)) continue;
                    int ms = 0;
                    if (m.Groups[3].Success)
                    {
                        var frac = m.Groups[3].Value; // .5 -> 500, .50 -> 500, .123 -> 123
                        if (frac.Length == 1) ms = int.Parse(frac) * 100;
                        else if (frac.Length == 2) ms = int.Parse(frac) * 10;
                        else ms = int.Parse(frac);
                    }
                    var ts = new TimeSpan(0, 0, min, sec, ms).Add(TimeSpan.FromMilliseconds(offsetMs));
                    if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
                    lines.Add(new LyricLine(ts, text));
                    anySync = true;
                }
            }
            if (!anySync) return new ParsedLyrics(false, Array.Empty<LyricLine>(), raw, source, offsetMs);
            lines.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            lines = ClearLines(lines);
            return new ParsedLyrics(true, lines, raw, source, offsetMs);
        }
        public static int FindActiveIndex(IReadOnlyList<LyricLine> lines, TimeSpan position)
        {
            int lo = 0, hi = lines.Count - 1, res = -1;
            while (lo <= hi) { int mid = (lo + hi) / 2; if (lines[mid].Timestamp <= position) { res = mid; lo = mid + 1; } else hi = mid - 1; }
            return res;
        }

        private static List<LyricLine> ClearLines(List<LyricLine> sorted)
        {
            // duplicate timestamps
            const int maxIter = 10;
            for (int iter = 0; iter < maxIter; iter++)
            {
                sorted.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                bool anyGroup = false;
                bool anyStagger = false;
                var next = new List<LyricLine>(sorted.Count);
                int i = 0;
                while (i < sorted.Count)
                {
                    int j = i + 1;
                    while (j < sorted.Count && sorted[j].Timestamp == sorted[i].Timestamp) j++;
                    int cnt = j - i;
                    if (cnt == 1)
                    {
                        next.Add(sorted[i]);
                    }
                    else
                    {
                        anyGroup = true;
                        var group = sorted.GetRange(i, cnt);
                        var nonEmpty = group.Where(l => !string.IsNullOrWhiteSpace(l.Text)).ToList();
                        int emptyCnt = cnt - nonEmpty.Count;

                        if (nonEmpty.Count > 0 && emptyCnt > 0)
                        {
                            if (nonEmpty.Count == 1)
                            {
                                next.Add(nonEmpty[0]);
                            }
                            else
                            {
                                for (int k = 0; k < nonEmpty.Count; k++)
                                {
                                    var ts = k == 0 ? nonEmpty[k].Timestamp : nonEmpty[k].Timestamp.Add(TimeSpan.FromSeconds(k));
                                    next.Add(new LyricLine(ts, nonEmpty[k].Text));
                                }
                                anyStagger = true;
                            }
                        }
                        else if (nonEmpty.Count > 0)
                        {
                            for (int k = 0; k < nonEmpty.Count; k++)
                            {
                                var ts = k == 0 ? nonEmpty[k].Timestamp : nonEmpty[k].Timestamp.Add(TimeSpan.FromSeconds(k));
                                next.Add(new LyricLine(ts, nonEmpty[k].Text));
                            }
                            anyStagger = true;
                        }
                        else
                        {
                            next.Add(group[0]);
                        }
                    }
                    i = j;
                }
                sorted = next;
                if (!anyGroup) break;
                if (!anyStagger) break;
            }
            sorted.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return sorted;
        }
    }
}
