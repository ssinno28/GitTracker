using System.Text.RegularExpressions;

namespace GitTracker.Helpers
{
    public static class StringHelpers
    {
        public static string MakeUrlFriendly(this string value)
        {
            return Regex.Replace(value, @"[^A-Za-z0-9_\.~]+", "-").ToLower();
        }

        public static string ToSentenceCase(this string str)
        {
            return Regex.Replace(str, "[a-z][A-Z]", m => $"{m.Value[0]} {m.Value[1]}");
        }
    }
}