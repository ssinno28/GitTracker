namespace GitTracker.Models
{
    public class GitMergeCommits
    {
        public string OurFile { get; set; }
        public string TheirFile { get; set; }
        public string BaseFile { get; set; }
        public GitDiff GitDiff { get; set; }
    }
}