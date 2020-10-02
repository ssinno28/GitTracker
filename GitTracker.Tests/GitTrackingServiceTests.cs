using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitTracker.Tests.Models;
using LibGit2Sharp;
using Xunit;

namespace GitTracker.Tests
{
    [Collection("Sequential")]
    public class GitTrackingServiceTests : BaseTest, IAsyncLifetime
    {
        private BlogPost _initialTrackedItem;

        [Fact]
        public async Task Test_Sync_On_New_Repo()
        {
            GitConfig.LocalPath = SecondLocalPath;

            await GitTrackingService.Sync(Email);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        [Fact]
        public async Task Test_Name_Already_Exists()
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                    await GitTrackingService.Create(new BlogPost()
                    {
                        Name = "Test Blog Post"
                    }));
        }

        [Fact]
        public async Task Test_Sync_On_Repo()
        {
            var trackedBlogPost =
                await GitTrackingService.Create(new BlogPost()
                {
                    Name = "My second blog post"
                });

            GitTrackingService.Stage(trackedBlogPost);
            GitRepo.Commit("My Second Commit", Email);
            await GitTrackingService.Publish(Email);

            GitConfig.LocalPath = SecondLocalPath;
            await GitTrackingService.Sync(Email);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Theirs()
        {
            GitConfig.LocalPath = SecondLocalPath;

            await GitTrackingService.Sync(Email);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.Body = "My Test Body 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email, CheckoutFileConflictStrategy.Theirs);
            Assert.True(result);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Ours()
        {
            GitConfig.LocalPath = SecondLocalPath;

            await GitTrackingService.Sync(Email);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.Body = "My Test Body 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email, CheckoutFileConflictStrategy.Ours);
            Assert.True(result);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Normal()
        {
            GitConfig.LocalPath = SecondLocalPath;

            await GitTrackingService.Sync(Email);

            GitConfig.LocalPath = LocalPath;

            _initialTrackedItem.SeoDescription = "My Test Seo Description";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            GitConfig.LocalPath = SecondLocalPath;

            _initialTrackedItem.SeoDescription = "My Test Seo Description 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email);
            Assert.False(result);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts();
            Assert.Equal(1, conflicts.Count);
            Assert.Equal(2, conflicts.First().ChangedProperties.Count);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Normal_Value_Provider()
        {
            GitConfig.LocalPath = SecondLocalPath;

            await GitTrackingService.Sync(Email);

            GitConfig.LocalPath = LocalPath;

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            string filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body");
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            GitConfig.LocalPath = SecondLocalPath;

            contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body 2");
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email);
            Assert.False(result);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts();
            Assert.Equal(1, conflicts.Count);
            Assert.Equal(2, conflicts.First().ChangedProperties.Count);
            Assert.Equal(1, conflicts.First().ValueProviderConflicts.Count);
        }

        [Fact]
        public async Task Test_Get_Diff_FromHead()
        {
            _initialTrackedItem.SeoDescription = "My New Seo Description";

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            string filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body");

            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            var diff = await GitTrackingService.GetTrackedItemDiffs();
            Assert.NotEmpty(diff);
            Assert.NotEmpty(diff.First().ValueProviderDiffs);
        }

        [Fact]
        public async Task Test_Get_Diff_For_Commit()
        {
            var diff = await GitTrackingService.GetTrackedItemDiffs(GitRepo.GetCurrentCommitId());
            Assert.NotEmpty(diff);
            Assert.Null(diff.First().Initial);
            Assert.Equal("Test Blog Post", diff.First().Final.Name);
        }

        [Fact]
        public async Task Test_Get_Diff_For_Commit_With_Delete()
        {
            await GitTrackingService.Delete(_initialTrackedItem);
            bool staged = GitTrackingService.Stage(_initialTrackedItem);
            Assert.True(staged);

            string commitId = GitRepo.Commit("My Second Commit", Email);
            Assert.NotNull(commitId);

            var diff =
                await GitTrackingService.GetTrackedItemDiffs(commitId);
            Assert.NotEmpty(diff);
            Assert.Null(diff.First().Final);
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