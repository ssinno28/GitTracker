using System;
using System.Collections.Generic;

namespace GitTracker.Models
{
    public class GitConfig
    {
        public string RemotePath { get; set; }
        public string Token { get; set; }
        public string WebhookSecret { get; set; }
        public IList<Type> TrackedTypes { get; set; }
    }
}