using System;

namespace GitTracker.Models
{
    public class GitCommit
    {
        public string Author { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Message { get; set; }
        public string Id { get; set; }
        public bool Published { get; set; }
    }
}