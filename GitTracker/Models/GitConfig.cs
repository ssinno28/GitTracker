namespace GitTracker.Models
{
    public class GitConfig
    {
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public string Token { get; set; }
        public string WebhookSecret { get; set; }
    }
}