using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Tests.Helpers;
using GitTracker.Tests.Models;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GitTracker.Tests
{
    public class BaseTest : IDisposable
    {
        protected readonly ServiceProvider ServiceProvider;
        protected readonly string LocalPath;
        protected readonly string SecondLocalPath;
        protected readonly string RemotePath;
        protected readonly IGitRepo GitRepo;
        protected readonly IGitTrackingService GitTrackingService;
        protected readonly GitConfig GitConfig;
        protected readonly IPathProvider PathProvider;

        protected string Email = "john.doe@gmail.com";
        protected string FirstCommitId;

        protected Mock<IUpdateOperation> UpdateOperationMock;
        protected Mock<IDeleteOperation> DeleteOperationMock;
        protected Mock<ICreateOperation> CreateOperationMock;
        protected Mock<ILocalPathFactory> LocalPathFactoryMock;

        public BaseTest()
        {
            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            LocalPath = Path.Combine(settingsPath, "local-repo");
            SecondLocalPath = Path.Combine(settingsPath, "local-2-repo");
            RemotePath = Path.Combine(settingsPath, "remote-repo");

            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Models.Tag), typeof(Category) };
            var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole())
                .AddGitTracking("test", RemotePath, string.Empty, contentTypes);

            UpdateOperationMock = new Mock<IUpdateOperation>();
            UpdateOperationMock.Setup(x => x.IsMatch(It.IsAny<Type>())).Returns(true);
            UpdateOperationMock.Setup(x => x.Update(It.IsAny<TrackedItem>())).Returns(Task.CompletedTask);
            
            CreateOperationMock = new Mock<ICreateOperation>();
            CreateOperationMock.Setup(x => x.IsMatch(It.IsAny<Type>())).Returns(true);
            CreateOperationMock.Setup(x => x.Create(It.IsAny<TrackedItem>())).Returns(Task.CompletedTask);
            
            DeleteOperationMock = new Mock<IDeleteOperation>();
            DeleteOperationMock.Setup(x => x.IsMatch(It.IsAny<Type>())).Returns(true);
            DeleteOperationMock.Setup(x => x.Delete(It.IsAny<TrackedItem>())).Returns(Task.CompletedTask);

            LocalPathFactoryMock = new Mock<ILocalPathFactory>();
            LocalPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(LocalPath);

            serviceCollection.Add(new ServiceDescriptor(typeof(IUpdateOperation), UpdateOperationMock.Object));
            serviceCollection.Add(new ServiceDescriptor(typeof(IDeleteOperation), DeleteOperationMock.Object));
            serviceCollection.Add(new ServiceDescriptor(typeof(ICreateOperation), CreateOperationMock.Object));
            serviceCollection.Add(new ServiceDescriptor(typeof(ILocalPathFactory), LocalPathFactoryMock.Object));

            ServiceProvider = serviceCollection.BuildServiceProvider();

            if (!Directory.Exists(RemotePath) || !Repository.IsValid(RemotePath))
            {
                Repository.Init(RemotePath, true);
            }

            if (!Directory.Exists(LocalPath) || !Repository.IsValid(LocalPath))
            {
                Repository.Init(LocalPath);
                var repo = new Repository(LocalPath);
                repo.Network.Remotes.Add("origin", RemotePath);
            }  
            
            if (!Directory.Exists(SecondLocalPath) || !Repository.IsValid(SecondLocalPath))
            {
                Repository.Init(SecondLocalPath);
                var repo = new Repository(SecondLocalPath);
                repo.Network.Remotes.Add("origin", RemotePath);
            }

            GitRepo = ServiceProvider.GetService<IGitRepo>();
            GitTrackingService = ServiceProvider.GetService<IGitTrackingService>();
            GitConfig = ServiceProvider.GetService<GitConfig>();
            PathProvider = ServiceProvider.GetService<IPathProvider>();
        }

        public void Dispose()
        {
            DirectoryHelper.DeleteDirectory(LocalPath);
            DirectoryHelper.DeleteDirectory(SecondLocalPath);
            DirectoryHelper.DeleteDirectory(RemotePath);
        }
    }
}
