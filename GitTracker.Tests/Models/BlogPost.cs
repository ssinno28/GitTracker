using System;
using System.Collections.Generic;
using GitTracker.Attributes;
using GitTracker.Models;

namespace GitTracker.Tests.Models
{
    public class BlogPost : TrackedItem
    {
        public DateTime PublishedDate { get; set; }
        public DateTimeOffset PublishedDateOffset { get; set; }
        public bool IsPublished { get; set; }
        [Markdown]
        public string Body { get; set; }
        public string SeoDescription { get; set; }
        public string SeoTitle { get; set; }
        public string Excerpt { get; set; }
        public string ThumbnailUrl { get; set; }
        public IList<string> TagIds { get; set; }
        public object Category { get; set; }
        public Category Category2 { get; set; }
        public IList<Tag> Tags { get; set; }
    }
}