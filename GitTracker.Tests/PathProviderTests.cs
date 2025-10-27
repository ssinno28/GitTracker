using System;
using System.IO;
using System.Reflection;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Providers;
using GitTracker.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GitTracker.Tests
{
    public class PathProviderTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly BlogPost _blogPost;
        private readonly string _localPath;
        private readonly Mock<ILocalPathFactory> _localPathFactoryMock;

        public PathProviderTests()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            _localPath = Path.Combine(settingsPath, "local-repo");
            _localPathFactoryMock = new Mock<ILocalPathFactory>();
            _localPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(_localPath);
            serviceCollection.Add(new ServiceDescriptor(typeof(ILocalPathFactory), _localPathFactoryMock.Object));
            serviceCollection.Add(new ServiceDescriptor(typeof(GitConfig), new GitConfig()));

            serviceCollection.AddScoped<IPathProvider, PathProvider>();

            _serviceProvider = serviceCollection.BuildServiceProvider();

            _blogPost = new BlogPost()
            {
                Name = "My Test Name"
            };
        }

        [Fact]
        public void Get_Tracked_Type_Path()
        {
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetTrackedItemPath(_blogPost.GetType());

            Assert.Equal(Path.Combine(_localPath, "BlogPost"), path);
        }        
        
        [Fact]
        public void Get_Tracked_Item_Path()
        {
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetTrackedItemPath(_blogPost.GetType(), _blogPost);

            Assert.Equal(Path.Combine(_localPath, "BlogPost", "my-test-name"), path);
        }

        [Fact]
        public void Get_Tracked_Item_Path_With_Null_ContentPath()
        {
            var gitConfig = _serviceProvider.GetService<GitConfig>();
            gitConfig.ContentPath = null;
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetTrackedItemPath(_blogPost.GetType(), _blogPost);

            Assert.Equal(Path.Combine(_localPath, "BlogPost", "my-test-name"), path);
        }

        [Fact]
        public void Get_Tracked_Type_Relative_Path()
        {
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetRelativeTrackedItemPath(_blogPost.GetType());

            Assert.Equal("BlogPost", path);
        }        
        
        [Fact]
        public void Get_Tracked_Item_Relative_Path()
        {
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetRelativeTrackedItemPath(_blogPost.GetType(), _blogPost);

            Assert.Equal("BlogPost/my-test-name", path);
        }

        [Fact]
        public void Get_Tracked_Item_Relative_Path_With_ContentPath()
        {
            var gitConfig = _serviceProvider.GetService<GitConfig>();
            gitConfig.ContentPath = "content";
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetRelativeTrackedItemPath(_blogPost.GetType(), _blogPost);
            Assert.Equal("content/BlogPost/my-test-name", path);
        }

        [Fact]
        public void Get_Tracked_Type_Relative_Path_With_ContentPath()
        {
            var gitConfig = _serviceProvider.GetService<GitConfig>();
            gitConfig.ContentPath = "content";
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetRelativeTrackedItemPath(_blogPost.GetType());
            Assert.Equal("content/BlogPost", path);
        }

        [Fact]
        public void Get_Tracked_Item_Relative_Path_With_Null_ContentPath()
        {
            var gitConfig = _serviceProvider.GetService<GitConfig>();
            gitConfig.ContentPath = null;
            var pathProvider = _serviceProvider.GetService<IPathProvider>();
            string path = pathProvider.GetRelativeTrackedItemPath(_blogPost.GetType(), _blogPost);
            Assert.Equal("BlogPost/my-test-name", path);
        }
    }
}