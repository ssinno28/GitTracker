using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Threading.Tasks;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using GitTracker.Services;
using GitTracker.Tests.Models;
using LibGit2Sharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GitTracker.Tests
{
    public class GitTrackingServiceTests
    {
        private readonly IGitTrackingService _gitTrackingService;

        private readonly Mock<IGitRepo> _mockGitRepo;
        private readonly Mock<IValueProvider> _mockValueProvider;
        private readonly Mock<IFileProvider> _mockFileProvider;
        private readonly Mock<IPathProvider> _mockPathProvider;
        private readonly Mock<IUpdateOperation> _mockUpdateOperation;
        private readonly Mock<ICreateOperation> _mockCreateOperation;
        private readonly Mock<IDeleteOperation> _mockDeleteOperation;
        private readonly Mock<ILocalPathFactory> _localPathFactoryMock;
        private readonly Mock<IFileSystem> _mockFileSystem;

        public GitTrackingServiceTests()
        {
            var contentTypes = new List<Type> { typeof(BlogPost), typeof(Models.Tag), typeof(Category) };
            var services = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            services.AddScoped<ContentContractResolver>();
            services.AddScoped<IGitTrackingService, GitTrackingService>();
            services.AddSingleton(new GitConfig
            {
                RemotePath = string.Empty,
                Token = string.Empty,
                TrackedTypes = contentTypes,
                WebhookSecret = string.Empty
            });

            _mockGitRepo = new Mock<IGitRepo>();
            _mockValueProvider = new Mock<IValueProvider>();
            _mockFileProvider = new Mock<IFileProvider>();
            _mockPathProvider = new Mock<IPathProvider>();
            _mockUpdateOperation = new Mock<IUpdateOperation>();
            _mockCreateOperation = new Mock<ICreateOperation>();
            _mockDeleteOperation = new Mock<IDeleteOperation>();
            _localPathFactoryMock = new Mock<ILocalPathFactory>();
            _mockFileSystem = new Mock<IFileSystem>();

            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            string localPath = Path.Combine(settingsPath, "local-repo");

            _localPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(localPath);

            services.Add(new ServiceDescriptor(typeof(ILocalPathFactory), _localPathFactoryMock.Object));
            services.Add(new ServiceDescriptor(typeof(IValueProvider), _mockValueProvider.Object));
            services.Add(new ServiceDescriptor(typeof(IGitRepo), _mockGitRepo.Object));
            services.Add(new ServiceDescriptor(typeof(IFileProvider), _mockFileProvider.Object));
            services.Add(new ServiceDescriptor(typeof(IPathProvider), _mockPathProvider.Object));
            services.Add(new ServiceDescriptor(typeof(IUpdateOperation), _mockUpdateOperation.Object));
            services.Add(new ServiceDescriptor(typeof(ICreateOperation), _mockCreateOperation.Object));
            services.Add(new ServiceDescriptor(typeof(IDeleteOperation), _mockDeleteOperation.Object));
            services.Add(new ServiceDescriptor(typeof(IFileSystem), _mockFileSystem.Object));

            _gitTrackingService = services.BuildServiceProvider().GetService<IGitTrackingService>();
        }

        [Fact]
        public async Task Test_Update_When_Diff_from_Head()
        {
            _mockValueProvider.Setup(x => x.IsMatch(It.IsAny<PropertyInfo>()))
                .Returns(false);

            _mockFileProvider.Setup(x => x.UpsertFiles(It.IsAny<TrackedItem[]>()))
                .Returns(Task.FromResult(true));

            _mockPathProvider
                .Setup(x => x.GetRelativeTrackedItemPath(It.IsAny<Type>(), It.IsAny<TrackedItem>()))
                .Returns("/test/path.json");

            var diffs = new List<GitDiff>
            {
                new GitDiff()
            };

            _mockGitRepo.Setup(x => x.GetDiffFromHead(It.IsAny<IList<string>>()))
                .Returns(diffs);

            _mockUpdateOperation.Setup(x => x.IsMatch(It.IsAny<Type>()))
                .Returns(true);

            var blogPostToUpdate = new BlogPost
            {
                Id = "1",
                Name = "Test"
            };

            var result =
                await _gitTrackingService.Update(blogPostToUpdate);

            _mockUpdateOperation.Verify(x => x.Update(It.IsAny<TrackedItem>()), Times.Once);
        }

        [Fact]
        public async Task Test_Publish_Does_Not_Catch_Generic_Exception()
        {
            _mockGitRepo.Setup(x =>
                    x.Pull(It.IsAny<string>(), It.IsAny<CheckoutFileConflictStrategy>(), It.IsAny<string>()))
                .Throws<Exception>();


            await Assert.ThrowsAnyAsync<Exception>(() => _gitTrackingService.Sync(It.IsAny<string>(), It.IsAny<CheckoutFileConflictStrategy>(), It.IsAny<string>()));
        }

        [Fact]
        public async Task Test_Publish_Throws_No_Exception_When_No_Branch_To_Pull()
        {
            _mockGitRepo.Setup(x =>
                    x.Pull(It.IsAny<string>(), It.IsAny<CheckoutFileConflictStrategy>(), It.IsAny<string>()))
                .Throws<MergeFetchHeadNotFoundException>();
            
            _mockGitRepo.Setup(x =>
                    x.Push(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockGitRepo.Setup(x => x.GetCommits(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IList<string>>()))
                .Returns(new List<GitCommit>());

            var diffs = new List<GitDiff>();
            _mockGitRepo.Setup(x => x.GetDiffFromHead(It.IsAny<IList<string>>()))
                .Returns(diffs);

            var result =
                await _gitTrackingService.Publish(It.IsAny<string>(), It.IsAny<CheckoutFileConflictStrategy>(), It.IsAny<string>());

            Assert.True(result);
        }

        [Fact]
        public void Test_Make_Url_Friendly()
        {
            string safeName = "Docklands Enterprise Ltd.".MakeUrlFriendly();
            Assert.Equal("docklands-enterprise-ltd", safeName);
        }

        [Fact]
        public async Task Test_Name_Already_Exists()
        {
            _mockFileSystem
                .Setup(x => x.Directory.Exists(It.IsAny<string>()))
                .Returns(true);

            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await _gitTrackingService.Create(new BlogPost
                {
                    Name = "Test Blog Post"
                }));
        }
    }
}