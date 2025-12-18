using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GitTracker.Tests.Models;
using LibGit2Sharp;
using Xunit;

namespace GitTracker.Tests
{
    [Collection("Sequential")]
    public class IntegrationTests : BaseTest, IAsyncLifetime
    {
        private BlogPost _initialTrackedItem;

        [Fact]
        public async Task Test_Sync_On_New_Repo()
        {
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            await GitTrackingService.Sync(Email);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        [Fact]
        public async Task Test_Get_History_For_TrackedItem()
        {
            var commits = GitTrackingService.GetHistory(_initialTrackedItem);
            Assert.Equal("My First Commit\n", commits.Commits.First().Message);
        }

        // libgit2sharp database not updating fast enough to show the moved file history
        //[Fact]
        // public async Task Test_Get_History_For_Moved_TrackedItem()
        // {
        //     var commits = GitTrackingService.GetHistory(_initialTrackedItem);
        //     Assert.Equal("My First Commit\n", commits.Commits.First().Message);

        //     var newBlogPost = await GitTrackingService.ChangeName("My New Name", _initialTrackedItem, Email);

        //     var newCommits = GitTrackingService.GetHistory(newBlogPost);
        //     Assert.Equal(3, newCommits.Count);
        // }

        [Fact]
        public async Task Test_Change_Name()
        {
            var blogPost = await GitTrackingService.ChangeName("Changed Name", _initialTrackedItem, Email);

            var newPath =
                PathProvider.GetRelativeTrackedItemPath(blogPost.GetType(), blogPost);

            Assert.Equal("BlogPost/changed-name", newPath);

            string fullPathToFile = Path.Combine(LocalPath, "BlogPost", "changed-name", $"{blogPost.Id}.json");
            Assert.True(File.Exists(fullPathToFile));
        }

        [Fact]
        public async Task Test_Change_Name_Fail()
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await GitTrackingService.ChangeName("Test Blog Post", _initialTrackedItem, Email));
        }

        [Fact]
        public async Task Test_Get_History_For_TrackedItemType()
        {
            var trackedBlogPost =
                await GitTrackingService.Create(new BlogPost()
                {
                    Name = "My second blog post"
                });

            GitTrackingService.Stage(trackedBlogPost);
            GitRepo.Commit("My Second Commit", Email);

            var commits = GitTrackingService.GetHistory(typeof(BlogPost));
            Assert.Equal(2, commits.Count);
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

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);
            await GitTrackingService.Sync(Email);

            var commits = GitRepo.GetCommits();
            Assert.NotEmpty(commits);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Theirs()
        {
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            await GitTrackingService.Sync(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

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
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            await GitTrackingService.Sync(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            _initialTrackedItem.Body = "My Test Body";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

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
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            await GitTrackingService.Sync(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            _initialTrackedItem.SeoDescription = "My Test Seo Description";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            _initialTrackedItem.SeoDescription = "My Test Seo Description 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email);
            Assert.False(result);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts();
            Assert.Equal(1, conflicts.Count);
        }

        [Fact]
        public async Task Test_Merge_On_Repo_Merge_Conflict()
        {
            await GitTrackingService.CreateBranch("test-branch");

            _initialTrackedItem.SeoDescription = "My Test Seo Description";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Test Branch Second Commit", Email);

            await GitTrackingService.SwitchBranch("master");

            _initialTrackedItem.SeoDescription = "My Test Seo Description 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Master Branch Second Commit", Email);

            bool failedMerge = await GitTrackingService.MergeBranch("test-branch", Email);
            Assert.False(failedMerge);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts();
            Assert.Equal(1, conflicts.Count);

            // take ours and merge
            var conflict = conflicts.First();
            await GitTrackingService.Update(conflict.Ours);
            GitTrackingService.Stage(conflict.Ours);
            GitRepo.Commit("Fixing Merge Conflict", Email);

            bool successfulMerge = await GitTrackingService.MergeBranch("test-branch", Email);
            Assert.True(successfulMerge);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Normal_Value_Provider()
        {
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            await GitTrackingService.Sync(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            string filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body");
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

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
            Assert.Equal(1, conflicts.First().ValueProviderConflicts.Count);
        }

        [Fact]
        public async Task Test_Sync_On_Repo_Merge_Conflict_Take_Normal_No_Value_Provider()
        {
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            await GitTrackingService.Sync(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            string filePath = Path.Combine(contentItemPath, "body.mds");

            await File.WriteAllTextAsync(filePath, "My Test Body");
            _initialTrackedItem.Category = "My test category";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 1 Second Commit", Email);
            await GitTrackingService.Publish(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body 2");
            _initialTrackedItem.Category = "My test category 2";
            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);

            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("Repo 2 Second Commit", Email);

            bool result = await GitTrackingService.Sync(Email);
            Assert.False(result);

            var conflicts = await GitTrackingService.GetTrackedItemConflicts();
            Assert.Equal(1, conflicts.Count);
            Assert.Equal(0, conflicts.First().ValueProviderConflicts.Count);
        }

        [Fact]
        public async Task Test_Get_Diff_FromHead_Update()
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
        public async Task Test_Get_Diff_FromHead_WithValueProvider_Create()
        {
            var trackedItem = await GitTrackingService.Create(new BlogPost()
            {
                Name = "My New Blog Post"
            });

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), trackedItem);
            string filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body");

            var diff = await GitTrackingService.GetTrackedItemDiffs();
            Assert.NotEmpty(diff);
            Assert.NotEmpty(diff.First().ValueProviderDiffs);
        }

        [Fact]
        public async Task Test_Reset_File_Changes()
        {
            var trackedItem = await GitTrackingService.Create(new BlogPost()
            {
                Name = "My New Blog Post"
            });

            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), trackedItem);
            string filePath = Path.Combine(contentItemPath, "body.md");

            await File.WriteAllTextAsync(filePath, "My Test Body");

            var diff = await GitTrackingService.GetTrackedItemDiffs();
            Assert.NotEmpty(diff);
            Assert.NotEmpty(diff.First().ValueProviderDiffs);

            await GitTrackingService.ResetFileChanges(diff.First());

            var resetDiff = await GitTrackingService.GetTrackedItemDiffs();
            Assert.Empty(resetDiff);

            Assert.False(Directory.Exists(contentItemPath));
        }


        // TODO: Figure out why test is flaking
        //[Fact]
        //public async Task Test_Get_Diff_WithValueProvider_Delete()
        //{
        //    string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
        //    string filePath = Path.Combine(contentItemPath, "body.md");

        //    await File.WriteAllTextAsync(filePath, "My Test Body");

        //    _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
        //    GitTrackingService.Stage(_initialTrackedItem);
        //    string secondCommit = GitRepo.Commit("My Second Commit", Email);

        //    await GitTrackingService.Delete(_initialTrackedItem);
        //    GitTrackingService.Stage(_initialTrackedItem);
        //    string commitId = GitRepo.Commit("My Third Commit", Email);

        //    var diff = await GitTrackingService.GetTrackedItemDiffs(commitId);
        //    Assert.NotEmpty(diff);
        //    Assert.NotEmpty(diff.First().ValueProviderDiffs);
        //}     

        [Fact]
        public async Task Test_Get_Diff_FromHead_NoValueProviderExtension()
        {
            string contentItemPath = PathProvider.GetTrackedItemPath(typeof(BlogPost), _initialTrackedItem);
            string filePath = Path.Combine(contentItemPath, "body.mds");

            await File.WriteAllTextAsync(filePath, "My Test Body");

            _initialTrackedItem = await GitTrackingService.Update(_initialTrackedItem);
            GitTrackingService.Stage(_initialTrackedItem);
            GitRepo.Commit("My Second Commit", Email);

            await GitTrackingService.Delete(_initialTrackedItem);

            var diff = await GitTrackingService.GetTrackedItemDiffs();
            Assert.NotEmpty(diff);
            Assert.Empty(diff.First().ValueProviderDiffs);
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

        [Fact]
        public async Task Test_Switch_Branch()
        {
            await GitTrackingService.CreateBranch("test-branch");

            await GitTrackingService.Delete(_initialTrackedItem);
            bool staged = GitTrackingService.Stage(_initialTrackedItem);
            Assert.True(staged);

            string commitId = GitRepo.Commit("My Second Commit", Email);
            Assert.NotNull(commitId);

            bool result = await GitTrackingService.SwitchBranch("master");
            Assert.True(result);

            bool switchBackResult = await GitTrackingService.SwitchBranch("test-branch");
            Assert.True(switchBackResult);
        }

        [Fact]
        public async Task Test_Switch_Branch_Fails()
        {
            await GitTrackingService.CreateBranch("test-branch");
            await GitTrackingService.Delete(_initialTrackedItem);

            await Assert.ThrowsAnyAsync<Exception>(async () => await GitTrackingService.SwitchBranch("master"));
        }

        [Fact]
        public async Task Test_Create_Branch_Fails()
        {
            await GitTrackingService.Delete(_initialTrackedItem);

            await Assert.ThrowsAnyAsync<Exception>(async () => await GitTrackingService.Create("test-branch"));
        }

        //[Fact]
        //public async Task Test_Stash()
        //{
        //    await GitTrackingService.Delete(_initialTrackedItem);
        //    var result = await GitTrackingService.Stash("My stash", Email, Email, _initialTrackedItem);

        //    Assert.True(result);
        //    Assert.True(Directory.Exists(PathProvider.GetTrackedItemPath(_initialTrackedItem.GetType(), _initialTrackedItem)));
        //}        

        [Fact]
        public async Task Test_Sync_Fails()
        {
            await GitTrackingService.Delete(_initialTrackedItem);
            await Assert.ThrowsAnyAsync<Exception>(async () => await GitTrackingService.Sync(Email));
        }

        [Fact]
        public async Task Test_DateModified_Unmodified()
        {
            DateTimeOffset currentModifedDate = _initialTrackedItem.ModifiedDate;
            BlogPost result = await GitTrackingService.Update(_initialTrackedItem);

            Assert.Equal(currentModifedDate, _initialTrackedItem.ModifiedDate);
        }

        public async Task InitializeAsync()
        {
            _initialTrackedItem = await GitTrackingService.Create(new BlogPost
            {
                Name = "Test Blog Post"
            });

            GitTrackingService.Stage(_initialTrackedItem);
            var trackedItemsCommitted = 
                await GitTrackingService.Commit("My First Commit", Email);
            _initialTrackedItem = (BlogPost) trackedItemsCommitted.First();

            GitRepo.Push(Email);
        }

        public async Task DisposeAsync()
        {
        }
    }
}