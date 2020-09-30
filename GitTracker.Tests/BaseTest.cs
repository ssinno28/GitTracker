using System;
using System.IO;
using System.Reflection;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Tests.Helpers;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GitTracker.Tests
{
    public class BaseTest : IDisposable
    {
        protected readonly ServiceProvider ServiceProvider;
        protected readonly string LocalPath;
        protected readonly string SecondLocalPath;
        protected readonly string RemotePath;
        protected readonly IGitRepo GitRepo;
        protected readonly GitConfig GitConfig;

        protected string Email = "john.doe@gmail.com";
        protected readonly string FirstCommitId;

        public BaseTest()
        {
            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            LocalPath = $"{settingsPath}\\local-repo";
            SecondLocalPath = $"{settingsPath}\\local-2-repo";
            RemotePath = $"{settingsPath}\\remote-repo";

            ServiceProvider = new ServiceCollection()
                .AddLogging(x => x.AddConsole())
                .AddGitTracking(LocalPath, "test", RemotePath, string.Empty)
                .BuildServiceProvider();

            if (!Directory.Exists(RemotePath) || !Repository.IsValid(RemotePath))
            {
                Repository.Init(RemotePath, true);
            }

            if (!Directory.Exists(LocalPath) || !Repository.IsValid(LocalPath))
            {
                Repository.Init(LocalPath);
            }  
            
            if (!Directory.Exists(SecondLocalPath) || !Repository.IsValid(SecondLocalPath))
            {
                Repository.Init(SecondLocalPath);
            }

            GitRepo = ServiceProvider.GetService<IGitRepo>();
            GitConfig = ServiceProvider.GetService<GitConfig>();
            
            string filePath = Path.Combine(LocalPath, "fileToCommit.txt");
            File.WriteAllText(filePath, "Testing service");

            GitRepo.Stage(filePath);
            FirstCommitId = GitRepo.Commit("first commit", Email);
        }

        public void Dispose()
        {
            DirectoryHelper.DeleteDirectory(LocalPath);
            DirectoryHelper.DeleteDirectory(SecondLocalPath);
            DirectoryHelper.DeleteDirectory(RemotePath);
        }
    }
}
