using GitTracker.Dictionary;

namespace GitTracker.Models
{
    public class GitDiffLine
    {
        public string Text { get; set; }
        public GitDiffLineType LineType { get; set; }
    }
}