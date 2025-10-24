using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Xunit;

namespace GitTracker.Tests
{
    [Collection("Sequential")]
    public class GitRepoTests : BaseTest
    {
        public GitRepoTests()
        {
            string filePath = Path.Combine(LocalPath, "fileToCommit.txt");
            File.WriteAllText(filePath, "Testing service");

            GitRepo.Stage(filePath);
            FirstCommitId = GitRepo.Commit("first commit", Email);
        }


        [Fact]
        public void Test_Get_Diff_From_Head()
        {
            var newContent = "Testing unstage files again";
            File.WriteAllText(Path.Combine(LocalPath, "fileToCommit.txt"), newContent);

            var diff = GitRepo.GetDiffFromHead();

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Modified, diff.First().ChangeKind);
        }

        [Fact]
        public void Test_Push()
        {
            bool result = GitRepo.Push(Email);
            Assert.True(result);
        }

        [Fact]
        public void Test_Pull()
        {
            GitRepo.Push(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);

            bool result = GitRepo.Pull(Email, CheckoutFileConflictStrategy.Normal);
            Assert.True(result);

            var commits = GitRepo.GetCommits();

            Assert.True(commits[0].Published);
            Assert.NotEmpty(commits);
        }

        [Fact]
        public void Test_Is_Commit_Not_Pushed()
        {
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            var commits = GitRepo.GetCommits();

            Assert.False(commits[0].Published);
            Assert.NotEmpty(commits);
        }

        [Fact]
        public void Test_Get_Diff_From_Multiple_Commits()
        {
            string filePath = Path.Combine(LocalPath, "fileToCommit2.txt");
            File.WriteAllText(filePath, "testing");
            GitRepo.Stage(filePath);

            string secondCommitId = GitRepo.Commit("first test commit", Email);

            string deleteFilePath = Path.Combine(LocalPath, "fileToCommit.txt");
            File.Delete(deleteFilePath);
            GitRepo.Stage(deleteFilePath);

            string thirdCommitId = GitRepo.Commit("second test commit", Email);

            var diff = GitRepo.GetDiff(new List<string>(), FirstCommitId, thirdCommitId);

            var deletedDiff = diff.First(x => x.Path.Equals("fileToCommit.txt"));
            var addedDiff = diff.First(x => x.Path.Equals("fileToCommit2.txt"));

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Added, addedDiff.ChangeKind);
            Assert.Equal(ChangeKind.Deleted, deletedDiff.ChangeKind);
        }

        [Fact]
        public void Test_Checkout_Deleted_Path()
        {
            string filePath = Path.Combine(LocalPath, "fileToCommit2.txt");
            File.WriteAllText(filePath, "testing");
            GitRepo.Stage(filePath);

            string secondCommitId = GitRepo.Commit("first test commit", Email);

            string deleteFilePath = Path.Combine(LocalPath, "fileToCommit.txt");
            File.Delete(deleteFilePath);
            GitRepo.Stage(deleteFilePath);

            string thirdCommitId = GitRepo.Commit("second test commit", Email);

            var diff = GitRepo.GetDiff(new List<string>(), FirstCommitId, thirdCommitId);

            var deletedDiff = diff.First(x => x.Path.Equals("fileToCommit.txt"));
            var addedDiff = diff.First(x => x.Path.Equals("fileToCommit2.txt"));

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Added, addedDiff.ChangeKind);
            Assert.Equal(ChangeKind.Deleted, deletedDiff.ChangeKind);

            GitRepo.CheckoutPaths(FirstCommitId, "fileToCommit.txt");
            Assert.True(File.Exists(Path.Combine(LocalPath, "fileToCommit.txt")));

            GitRepo.Reset(ResetMode.Hard);
            Assert.False(File.Exists(Path.Combine(LocalPath, "fileToCommit.txt")));
        }

        [Fact]
        public void Test_Get_Diff_From_Commit()
        {
            var diff = GitRepo.GetDiff(new List<string>(), FirstCommitId);
            var addedDiff = diff.First(x => x.Path.Equals("fileToCommit.txt"));

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Added, addedDiff.ChangeKind);
        }

        [Fact]
        public void TestGetUnstagedFiles()
        {
            var content = "Testing unstage files";
            File.WriteAllText(Path.Combine(LocalPath, "fileToCommit.txt"), content);

            var untrackedFiles = GitRepo.GetUnstagedItems();

            GitRepo.Stage(untrackedFiles.ToArray());
            GitRepo.Unstage(untrackedFiles.ToArray());

            untrackedFiles = GitRepo.GetUnstagedItems();

            Assert.Equal(1, untrackedFiles.Count);
        }

        [Fact]
        public void Test_Revert_Commit()
        {
            var revertStatus = GitRepo.RevertCommit(FirstCommitId, Email);
            Assert.Equal(RevertStatus.Reverted, revertStatus);
        }

        [Fact]
        public void TestGetStagedFiles()
        {
            var content = "Testing stage files";
            string filePath = Path.Combine(LocalPath, "fileToCommit.txt");
            File.WriteAllText(filePath, content);

            GitRepo.Stage("fileToCommit.txt");
            var stagedFiles = GitRepo.GetStagedItems();

            Assert.Equal(1, stagedFiles.Count);
        }

        [Fact]
        public void Test_Get_Diff_From_Head_For_Path()
        {
            var content = "Testing diff from head";
            string filePath = Path.Combine(LocalPath, "fileToCommit.txt");
            File.WriteAllText(filePath, content);

            var diff = GitRepo.GetDiffFromHead(new List<string> { "fileToCommit.txt" });
            Assert.Equal(1, diff.Count);
        }

        [Fact]
        public void Test_Repository_Exists()
        {
            Assert.True(GitRepo.RepositoryExists());
        } 
        
        [Fact]
        public void Test_Repository_NotExists()
        {
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns("fake-path");
            Assert.False(GitRepo.RepositoryExists());
        }

        [Fact]
        public void Test_Pull_On_No_Remote_Branch()
        {
            GitRepo.CreateBranch("my-test-branch");
            Assert.Throws<MergeFetchHeadNotFoundException>(() => GitRepo.Pull(Email, CheckoutFileConflictStrategy.Normal));
        }

        [Fact]
        public void Test_Reset_File_Changes()
        {
            var originalContent = "Testing service";
            var modifiedContent = "Modified content for reset test";
            string filePath = Path.Combine(LocalPath, "fileToCommit.txt");
            
            // Modify the file that was committed in constructor
            File.WriteAllText(filePath, modifiedContent);
            
            // Verify file is modified
            var currentContent = File.ReadAllText(filePath);
            Assert.Equal(modifiedContent, currentContent);
            
            // Reset the file changes
            bool result = GitRepo.ResetFileChanges("fileToCommit.txt");
            
            // Verify the reset was successful
            Assert.True(result);
            
            // Verify file content is restored to original
            var restoredContent = File.ReadAllText(filePath);
            Assert.Equal(originalContent, restoredContent);
        }

        [Fact]
        public void Test_Create_Merge_Conflict_Commit()
        {
            string filePath = Path.Combine(LocalPath, "fileToCommit.txt");
            GitRepo.Push(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);
            GitRepo.Pull(Email, CheckoutFileConflictStrategy.Normal);

            string secondFilePath = Path.Combine(SecondLocalPath, "fileToCommit.txt");
            var modifiedContent = "Modified content for reset test";

            // Modify the file that was committed in constructor
            File.WriteAllText(secondFilePath, modifiedContent);
            GitRepo.Stage(secondFilePath);
            var secondCommit = GitRepo.Commit("second commit", Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);
            var modifiedContentAgain = "Modified content for reset test again";
            File.WriteAllText(filePath, modifiedContentAgain);
            GitRepo.Stage(filePath);
            var thirdCommit = GitRepo.Commit("third commit", Email);
            GitRepo.Push(Email);

            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(SecondLocalPath);
            GitRepo.Pull(Email, CheckoutFileConflictStrategy.Normal);

            string message = GitRepo.GetMergeCommitMessage();
            
            Assert.Contains(@"Conflicts:", message);
        }

        [Fact]
        public void Test_Initialize_Repository()
        {
            string newRepoPath = Path.Combine(Path.GetTempPath(), "TestInitRepo", Guid.NewGuid().ToString());
            Directory.CreateDirectory(newRepoPath);
            
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(newRepoPath);
            
            bool result = GitRepo.InitializeRepository(newRepoPath);
            
            Assert.True(result);
            Assert.True(Directory.Exists(Path.Combine(newRepoPath, ".git")));
            
            // Cleanup
            Directory.Delete(newRepoPath, true);
        }

        [Fact]
        public void Test_Initialize_Bare_Repository()
        {
            string newRepoPath = Path.Combine(Path.GetTempPath(), "TestInitBareRepo", Guid.NewGuid().ToString());
            Directory.CreateDirectory(newRepoPath);
            
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(newRepoPath);
            
            bool result = GitRepo.InitializeRepository(newRepoPath, bare: true);
            
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(newRepoPath, "HEAD")));
            Assert.True(Directory.Exists(Path.Combine(newRepoPath, "objects")));
            Assert.True(Directory.Exists(Path.Combine(newRepoPath, "refs")));
            
            // Cleanup
            Directory.Delete(newRepoPath, true);
        }
    }
}
