using System;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Providers;
using GitTracker.Serializer;
using GitTracker.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GitTracker.Tests
{
    public class FileProviderTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly BlogPost _blogPost;
        private readonly string _localPath;
        private readonly Mock<ILocalPathFactory> _localPathFactoryMock;
        private readonly Mock<IFileSystem> _mockFileSystem;
        private readonly IFileProvider _fileProvider;

        public FileProviderTests()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            _localPath = Path.Combine(settingsPath, "local-repo");
            _localPathFactoryMock = new Mock<ILocalPathFactory>();
            _mockFileSystem = new Mock<IFileSystem>();

            _localPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(_localPath);
            serviceCollection.Add(new ServiceDescriptor(typeof(ILocalPathFactory), _localPathFactoryMock.Object));
            serviceCollection.Add(new ServiceDescriptor(typeof(IFileSystem), _mockFileSystem.Object));
            serviceCollection.Add(new ServiceDescriptor(typeof(GitConfig), new GitConfig()));

            serviceCollection.AddScoped<ContentContractResolver>();
            serviceCollection.AddScoped<IPathProvider, PathProvider>();
            serviceCollection.AddScoped<IFileProvider, FileProvider>();

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _blogPost = new BlogPost
            {
                Name = "My Test Name"
            };

            _fileProvider = _serviceProvider.GetService<IFileProvider>();
        }

        [Fact]
        public void Test_Get_Tracked_Item_Json()
        {
            string trackedItemPath = Path.Combine(_localPath, $"my-test-post/{Guid.NewGuid()}.json");
            string markdownPath = Path.Combine(_localPath, $"my-test-post/body.md");
            _mockFileSystem
                .Setup(x => 
                    x.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.Is<SearchOption>((option => option == SearchOption.AllDirectories))))
                .Returns(new string[]
                {
                    markdownPath,
                    trackedItemPath
                });

            Assert.Equal(trackedItemPath, _fileProvider.GetTrackedItemJsonForPath(markdownPath));
        }
    }
}