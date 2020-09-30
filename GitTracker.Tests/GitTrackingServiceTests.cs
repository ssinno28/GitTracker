using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Tests.Models;
using Moq;
using Xunit;

namespace GitTracker.Tests
{
    public class GitTrackingServiceTests : BaseTest, IAsyncLifetime
    {
        [Fact]
        public async Task Test_Sync_On_New_Repo()
        {
            GitRepo.Push(Email);

            GitConfig.LocalPath = SecondLocalPath;

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
            await GitTrackingService.Sync(Email, contentTypes);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        [Fact]
        public async Task Test_Sync_On_Repo()
        {
            GitRepo.Push(Email);

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
            GitRepo.Push(Email);

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

        public async Task InitializeAsync()
        {
            var trackedItem = await GitTrackingService.Add(new BlogPost
            {
                Name = "Test Blog Post"
            });

            GitTrackingService.Stage(trackedItem);
            GitRepo.Commit("My First Commit", Email);
        }

        public async Task DisposeAsync()
        {
        }
    }
}