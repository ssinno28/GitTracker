using System.Collections.Generic;

namespace GitTracker.Models
{
    public class GitDiffChangeCalculations
    {
        public GitDiffChangeCalculations()
        {
            GitDiffLines = new List<GitDiffLine>();
        }
        public string ChangeCalculations { get; set; }
        public IList<GitDiffLine> GitDiffLines { get; set; }
    }
}