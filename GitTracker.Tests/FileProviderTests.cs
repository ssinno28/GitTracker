using System;
using System.IO;
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

        public FileProviderTests()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            _localPath = Path.Combine(settingsPath, "local-repo");
            _localPathFactoryMock = new Mock<ILocalPathFactory>();
            _localPathFactoryMock.Setup(x => x.GetLocalPath()).Returns(_localPath);
            serviceCollection.Add(new ServiceDescriptor(typeof(ILocalPathFactory), _localPathFactoryMock.Object));

            serviceCollection.AddScoped<ContentContractResolver>();
            serviceCollection.AddScoped<IPathProvider, PathProvider>();
            serviceCollection.AddScoped<IFileProvider, FileProvider>();

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _blogPost = new BlogPost
            {
                Name = "My Test Name"
            };
        }
    }
}