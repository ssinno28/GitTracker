using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Tests.Models;
using GitTracker.ValueProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GitTracker.Tests
{
    public class ValueProviderTests
    {

        private readonly Mock<IGitRepo> _mockGitRepo;
        private readonly Mock<IPathProvider> _mockPathProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly BlogPost _blogPost = new BlogPost();
        private readonly IFixture _fixture = new Fixture();

        public ValueProviderTests()
        {
            var services = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            _mockGitRepo = new Mock<IGitRepo>();
            _mockPathProvider = new Mock<IPathProvider>();

            services.Add(new ServiceDescriptor(typeof(IGitRepo), _mockGitRepo.Object));
            services.Add(new ServiceDescriptor(typeof(IPathProvider), _mockPathProvider.Object));
            services.AddScoped<IValueProvider, CreatedDateValueProvider>();
            services.AddScoped<IValueProvider, ModifiedDateValueProvider>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task Test_CreatedDateValueProvider()
        {
            var createdDateValueProvider =
                _serviceProvider.GetServices<IValueProvider>()
                    .First(x => x.GetType() == typeof(CreatedDateValueProvider));

            var propertyInfo = typeof(BlogPost).GetProperty(nameof(TrackedItem.CreatedDate));
            Assert.True(createdDateValueProvider.IgnoreInJson);
            Assert.True(createdDateValueProvider.IsMatch(propertyInfo));

            var gitCommits = _fixture.Create<IList<GitCommit>>();
            _mockGitRepo
                .Setup(x => x.GetAllCommitsForPath(It.IsAny<string>()))
                .Returns(gitCommits);

           var value = await createdDateValueProvider.GetValue(_blogPost, propertyInfo);
           Assert.Equal(gitCommits.Min(x => x.Date), value);
        }
        
        [Fact]
        public async Task Test_ModifiedDateValueProvider()
        {
            var createdDateValueProvider =
                _serviceProvider.GetServices<IValueProvider>()
                    .First(x => x.GetType() == typeof(ModifiedDateValueProvider));

            var propertyInfo = typeof(BlogPost).GetProperty(nameof(TrackedItem.ModifiedDate));
            Assert.True(createdDateValueProvider.IgnoreInJson);
            Assert.True(createdDateValueProvider.IsMatch(propertyInfo));

            var gitCommits = _fixture.Create<IList<GitCommit>>();
            _mockGitRepo
                .Setup(x => x.GetAllCommitsForPath(It.IsAny<string>()))
                .Returns(gitCommits);

           var value = await createdDateValueProvider.GetValue(_blogPost, propertyInfo);
           Assert.Equal(gitCommits.Max(x => x.Date), value);
        }
    }
}