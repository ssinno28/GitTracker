using System;
using System.IO;

namespace GitTracker.Helpers
{
    public static class PathHelpers
    {
        public static bool IsTrackedItemJson(this string path)
        {
            return Guid.TryParse(Path.GetFileNameWithoutExtension(path), out _);
        }
    }
}