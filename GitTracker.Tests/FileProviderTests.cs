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

        public FileProviderTests()
        {
            var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            string settingsPath
                = Path.GetFullPath(Path.Combine($"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}", @"..\..\..\settings"));

            _localPath = Path.Combine(settingsPath, "local-repo");
            var gitConfig = new GitConfig
            {
                LocalPath = _localPath
            };

            serviceCollection.AddScoped<ContentContractResolver>();
            serviceCollection.AddScoped<IPathProvider, PathProvider>();
            serviceCollection.AddScoped<IFileProvider, FileProvider>();
            serviceCollection.AddSingleton(gitConfig);

            _serviceProvider = serviceCollection.BuildServiceProvider();
            _blogPost = new BlogPost
            {
                Name = "My Test Name"
            };
        }
    }
}