using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace MusicWrap.Data.Helpers
{
    public static class TrackStringPool
    {
        private static readonly StringPool Pool = new();
        private static readonly object _lock = new();

        public static string? Intern(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            lock (_lock)
            {
                return Pool.GetOrAdd(value);
            }
        }
        public static ImmutableArray<string> Intern(string[]? values)
        {
            if (values is not null && values.Length > 0)
            {
                return [.. values
                    .Select(v=>Intern(v))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)];
            }
            return [];

        }
        public static ImmutableArray<string> Intern(ImmutableArray<string> values)
        {
            if (values.IsDefaultOrEmpty) return ImmutableArray<string>.Empty;
            return [.. values
                    .Select(v=>Intern(v))
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)];
        }
    }
}
