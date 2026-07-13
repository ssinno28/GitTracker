using System;
using System.IO;
using System.Threading.Tasks;
using GitTracker.Interfaces;
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
        private readonly Mock<IPathProvider> _mockPathProvider;
        private readonly IServiceProvider _serviceProvider;
        private readonly BlogPost _blogPost = new BlogPost();

        public ValueProviderTests()
        {
            var services = new ServiceCollection()
                .AddLogging(x => x.AddConsole());

            _mockPathProvider = new Mock<IPathProvider>();

            services.Add(new ServiceDescriptor(typeof(IPathProvider), _mockPathProvider.Object));
            services.AddScoped<IValueProvider, MarkdownValueProvider>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void Test_MarkdownValueProvider_IsMatch_And_Metadata()
        {
            var markdownValueProvider = GetMarkdownValueProvider();

            var markdownProperty = typeof(BlogPost).GetProperty(nameof(BlogPost.Body));
            var nonMarkdownProperty = typeof(BlogPost).GetProperty(nameof(BlogPost.Name));

            Assert.True(markdownValueProvider.IgnoreInJson);
            Assert.Equal(".md", markdownValueProvider.Extension);
            Assert.True(markdownValueProvider.IsMatch(markdownProperty));
            Assert.False(markdownValueProvider.IsMatch(nonMarkdownProperty));
        }

        [Fact]
        public async Task Test_MarkdownValueProvider_GetValue_Returns_Content_When_File_Exists()
        {
            var markdownValueProvider = GetMarkdownValueProvider();

            var propertyInfo = typeof(BlogPost).GetProperty(nameof(BlogPost.Body));
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            try
            {
                _mockPathProvider.Setup(x => x.GetTrackedItemPath(typeof(BlogPost), _blogPost))
                    .Returns(tempRoot);

                string markdownFilePath = Path.Combine(tempRoot, "body.md");
                const string expectedContent = "# test markdown";
                File.WriteAllText(markdownFilePath, expectedContent);

                var value = await markdownValueProvider.GetValue(_blogPost, propertyInfo);

                Assert.Equal(expectedContent, value);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        [Fact]
        public async Task Test_MarkdownValueProvider_GetValue_Returns_Empty_When_File_Does_Not_Exist()
        {
            var markdownValueProvider = GetMarkdownValueProvider();

            var propertyInfo = typeof(BlogPost).GetProperty(nameof(BlogPost.Body));
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempRoot);

            try
            {
                _mockPathProvider.Setup(x => x.GetTrackedItemPath(typeof(BlogPost), _blogPost))
                    .Returns(tempRoot);

                var value = await markdownValueProvider.GetValue(_blogPost, propertyInfo);

                Assert.Equal(string.Empty, value);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, true);
                }
            }
        }

        private IValueProvider GetMarkdownValueProvider()
        {
            foreach (var valueProvider in _serviceProvider.GetServices<IValueProvider>())
            {
                if (valueProvider.GetType() == typeof(MarkdownValueProvider))
                {
                    return valueProvider;
                }
            }

            throw new InvalidOperationException("MarkdownValueProvider not registered.");
        }
    }
}