using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitTracker.Tests.Models;
using LibGit2Sharp;
using Xunit;
using Tag = GitTracker.Tests.Models.Tag;

namespace GitTracker.Tests
{
    public class GitTrackingServiceTests : BaseTest, IAsyncLifetime
    {
        private BlogPost _initialTrackedItem;

        [Fact]
        public async Task Test_Sync_On_New_Repo()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        [Fact]
        public async Task Test_Sync_On_Repo()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            GitConfig.LocalPath = LocalPath;

            var trackedBlogPost =
                await GitTrackingService.Create(new BlogPost()
                {
                    Name = "My second blog post"
                });

            GitTrackingService.Stage(trackedBlogPost);
            GitRepo.Commit("My Second Commit", Email);
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;
            await GitTrackingService.Sync(Email, contentTypes);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }        
        
        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Theirs()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.Body = "My Test Body 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email, contentTypes, CheckoutFileConflictStrategy.Theirs);
            Assert.True(result);
        }        
        
        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Ours()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.Body = "My Test Body 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email, contentTypes, CheckoutFileConflictStrategy.Ours);
            Assert.True(result);
        }       
        
        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Normal()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.SeoDescription = "My Test Seo Description";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.SeoDescription = "My Test Seo Description 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email, contentTypes);
            Assert.False(result);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts(contentTypes);
            Assert.Equal(1, conflicts.Count);
            Assert.Equal(2, conflicts.First().ChangedProperties.Count);
        }        
        
        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Normal_Value_Provider()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            GitConfig.LocalPath = LocalPath;

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            string filePath = $"{contentItemPath}\\body.md";

            await File.WriteAllTextAsync(filePath, "My Test Body");
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;

            contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            filePath = $"{contentItemPath}\\body.md";

            await File.WriteAllTextAsync(filePath, "My Test Body 2");
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email, contentTypes);
            Assert.False(result);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts(contentTypes);
            Assert.Equal(1, conflicts.Count);
            Assert.Equal(2, conflicts.First().ChangedProperties.Count);
            Assert.Equal(1, conflicts.First().ValueProviderConflicts.Count);
        }

        public async Task InitializeAsync()
        {
            _initialTrackedItem = await GitTrackingService.Create(new BlogPost
            {
                Name = "Test Blog Post"
            });

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("My First Commit", Email);
            GitRepo.Push(Email);
        }

        public async Task DisposeAsync()
        {
        }
    }
}