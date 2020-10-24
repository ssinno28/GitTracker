using System.Text.RegularExpressions;

namespace GitTracker.Helpers
{
    public static class StringHelpers
    {
        public static string MakeUrlFriendly(this string value)
        {
            string urlFriendly = Regex.Replace(value, @"[^A-Za-z0-9_~]+", "-").ToLower();
            urlFriendly = urlFriendly.EndsWith("-") ? urlFriendly.Substring(0, urlFriendly.Length - 1) : urlFriendly;
            return urlFriendly;
        }

        public static string ToSentenceCase(this string str)
        {
            return Regex.Replace(str, "[a-z][A-Z]", m => $"{m.Value[0]} {m.Value[1]}");
        }
    }
}