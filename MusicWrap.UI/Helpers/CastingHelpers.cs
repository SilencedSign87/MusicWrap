namespace MusicWrap.UI.Helpers
{
    public static class CastingHelpers
    {
        public static bool TryToInt(object value, out int result)
        {
            if (value is int i)
            {
                result = i;
                return true;
            }

            if (value is string s && int.TryParse(s, out var parsed))
            {
                result = parsed;
                return true;
            }

            result = 0;
            return false;
        }
    }
}
