using System.Linq;

namespace MusicWrap.Data.Helpers
{
    public static class TrackStringPool
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Pool = new(System.StringComparer.Ordinal);

        public static string? Intern(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            return Pool.GetOrAdd(value, value);
        }

        public static string[] Intern(string[]? values)
        {
            if (values is null || values.Length == 0) return [];
            return [.. values
                .Select(v => Intern(v))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s!)];
        }
    }
}
