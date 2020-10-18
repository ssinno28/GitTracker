using System;
using System.Collections.Generic;
using GitTracker.Attributes;
using GitTracker.Helpers;
using GitTracker.Interfaces;

namespace GitTracker.Tests.Models
{
    public class BlogPost : ITrackedItem
    {
        [Markdown]
        public string Body { get; set; }
        public string SeoDescription { get; set; }
        public string SeoTitle { get; set; }
        public string Excerpt { get; set; }
        public string ThumbnailUrl { get; set; }
        public IList<string> Tags { get; set; }
        public string Category { get; set; }
        public string SafeName => Name.MakeUrlFriendly();
        public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);
        public string Id { get; set; }
        public string Name { get; set; }
        public string TypeDefinition { get; set; }
        public DateTimeOffset CreatedDate { get; set; }
        public DateTimeOffset ModifiedDate { get; set; }
        public IList<string> PreviousPaths { get; set; }
    }
}