using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Tests.Helpers;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GitTracker.Tests
{
    public class GitRepoTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly string _localPath;
        private readonly string _remotePath;
        private readonly IGitRepo _gitRepo;

        private string _email = "john.doe@gmail.com";
        private readonly string _firstCommitId;

        public GitRepoTests()
        {
            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            _localPath = $"{settingsPath}\\local-repo";
            _remotePath = $"{settingsPath}\\remote-repo";

            _serviceProvider = new ServiceCollection()
                .AddLogging(x => x.AddConsole())
                .AddGitTracking(_localPath, "test", _remotePath, string.Empty)
                .BuildServiceProvider();

            if (!Directory.Exists(_remotePath) || !Repository.IsValid(_remotePath))
            {
                Repository.Init(_remotePath, true);
            }

            if (!Directory.Exists(_localPath) || !Repository.IsValid(_localPath))
            {
                Repository.Init(_localPath);
            }

            _gitRepo = _serviceProvider.GetService<IGitRepo>();
            
            string filePath = Path.Combine(_localPath, "fileToCommit.txt");
            File.WriteAllText(filePath, "Testing service");

            _gitRepo.Stage(filePath);
            _firstCommitId = _gitRepo.Commit("first commit", _email);
        }

        [Fact]
        public void Test_Get_Diff_From_Head()
        {
            var newContent = "Testing unstage files again";
            File.WriteAllText(Path.Combine(_localPath, "fileToCommit.txt"), newContent);

            var diff = _gitRepo.GetDiffFromHead();

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Modified, diff.First().ChangeKind);
        }

        [Fact]
        public void Test_Push()
        {
            bool result = _gitRepo.Push(_email);
            Assert.True(result);
        }

        [Fact]
        public void Test_Get_Diff_From_Multiple_Commits()
        {
            string filePath = Path.Combine(_localPath, "fileToCommit2.txt");
            File.WriteAllText(filePath, "testing");
            _gitRepo.Stage(filePath);

            string secondCommitId = _gitRepo.Commit("first test commit", _email);

            string deleteFilePath = Path.Combine(_localPath, "fileToCommit.txt");
            File.Delete(deleteFilePath);
            _gitRepo.Stage(deleteFilePath);

            string thirdCommitId = _gitRepo.Commit("second test commit", _email);

            var diff = _gitRepo.GetDiff(new List<string>(), _firstCommitId, thirdCommitId);

            var deletedDiff = diff.First(x => x.Path.Equals("fileToCommit.txt"));
            var addedDiff = diff.First(x => x.Path.Equals("fileToCommit2.txt"));

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Added, addedDiff.ChangeKind);
            Assert.Equal(ChangeKind.Deleted, deletedDiff.ChangeKind);
        }

        [Fact]
        public void Test_Get_Diff_From_Commit()
        {
            var diff = _gitRepo.GetDiff(new List<string>(), _firstCommitId);
            var addedDiff = diff.First(x => x.Path.Equals("fileToCommit.txt"));

            Assert.NotEmpty(diff);
            Assert.Equal(ChangeKind.Added, addedDiff.ChangeKind);
        }

        [Fact]
        public void TestGetUnstagedFiles()
        {
            var content = "Testing unstage files";
            File.WriteAllText(Path.Combine(_localPath, "fileToCommit.txt"), content);

            var gitRepo = _serviceProvider.GetService<IGitRepo>();
            var untrackedFiles = gitRepo.GetUnstagedItems();

            gitRepo.Stage(untrackedFiles.ToArray());
            gitRepo.Unstage(untrackedFiles.ToArray());

            untrackedFiles = gitRepo.GetUnstagedItems();

            Assert.Equal(1, untrackedFiles.Count);
        }

        [Fact]
        public void TestGetStagedFiles()
        {
            var content = "Testing stage files";
            string filePath = Path.Combine(_localPath, "fileToCommit.txt");
            File.WriteAllText(filePath, content);

            var gitRepo = _serviceProvider.GetService<IGitRepo>();
            gitRepo.Stage("fileToCommit.txt");

            var stagedFiles = gitRepo.GetStagedItems();

            Assert.Equal(1, stagedFiles.Count);
        }

        public void Dispose()
        {
            DirectoryHelper.DeleteDirectory(_localPath);
            DirectoryHelper.DeleteDirectory(_remotePath);
        }
    }
}
