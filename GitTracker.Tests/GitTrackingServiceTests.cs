using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;
using GitTracker.Tests.Models;
using Xunit;

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
                await GitTrackingService.Add(new BlogPost()
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
        public async Task Test_Sync_On_Repo_Merge_Conflict()
        {
            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("My Second Commit", Email);
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.Body = "My Test Body 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("My Second Commit", Email);

            GitRepo.Pull(Email);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        public async Task InitializeAsync()
        {
            _initialTrackedItem = await GitTrackingService.Add(new BlogPost
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