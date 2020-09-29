using System.Collections.Generic;
using LibGit2Sharp;

namespace GitTracker.Models
{
    public class GitDiff
    {
        public GitDiff()
        {
            GitDiffChangeCalculations = new List<GitDiffChangeCalculations>();
        }

        public IList<GitDiffChangeCalculations> GitDiffChangeCalculations { get; set; }
        public string FileDiff { get; set; }
        public string Index { get; set; }
        public string InitialFile { get; set; }
        public string FinalFile { get; set; }
        public string Path { get; set; }
        public ChangeKind ChangeKind { get; set; }
    }
}