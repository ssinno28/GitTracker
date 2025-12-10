using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
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
        public async Task Test_Sync_Returns_True_When_CheckRemoteHasCommits_Returns_False()
        {
            // Arrange
            _mockGitRepo.Setup(x => x.CheckRemoteHasCommits(It.IsAny<string>()))
                .Returns(false);

            _mockGitRepo.Setup(x => x.GetDiffFromHead(It.IsAny<IList<string>>()))
                .Returns(new List<GitDiff>());

            // Act
            var result = await _gitTrackingService.Sync("test@example.com", CheckoutFileConflictStrategy.Normal, "testuser");

            // Assert
            Assert.True(result);
            _mockGitRepo.Verify(x => x.CheckRemoteHasCommits(It.IsAny<string>()), Times.Once);
            _mockGitRepo.Verify(x => x.Pull(It.IsAny<string>(), It.IsAny<CheckoutFileConflictStrategy>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Test_Publish_Calls_Push_When_Sync_Returns_True()
        {
            // Arrange
            _mockGitRepo.Setup(x => x.CheckRemoteHasCommits(It.IsAny<string>()))
                .Returns(true);

            _mockGitRepo.Setup(x =>
                    x.Pull(It.IsAny<string>(), It.IsAny<CheckoutFileConflictStrategy>(), It.IsAny<string>()))
                .Returns(true);

            _mockGitRepo.Setup(x => x.GetDiffFromHead(It.IsAny<IList<string>>()))
                .Returns(new List<GitDiff>());

            _mockGitRepo.Setup(x => x.GetDiff(It.IsAny<IList<string>>(), It.IsAny<string>(), null))
                .Returns(new List<GitDiff>());

            _mockGitRepo.Setup(x =>
                    x.Push(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockGitRepo.Setup(x => x.GetCommits(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IList<string>>()))
                .Returns(new List<GitCommit>()
                {
                    new GitCommit()
                });

            // Act
            var result = await _gitTrackingService.Publish("test@example.com", CheckoutFileConflictStrategy.Normal, "testuser");

            // Assert
            Assert.True(result);
            _mockGitRepo.Verify(x => x.Push(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
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

        [Fact]
        public async Task Test_GetTrackedItemDiffs_Returns_TrackedItem_When_Type_Not_In_Config()
        {
            // Arrange - Create a type not in GitConfig.TrackedTypes
            var unknownType = typeof(UnknownTrackedItem);
            
            _mockPathProvider
                .Setup(x => x.GetRelativeTrackedItemPath(unknownType, It.IsAny<TrackedItem>()))
                .Returns($"/unknown/{Guid.NewGuid().ToString()}.json");

            var diffs = new List<GitDiff>
            {
                new GitDiff
                {
                    Path = $"/unknown/{Guid.NewGuid().ToString()}.json",
                    ChangeKind = ChangeKind.Modified,
                    InitialFileContent = "{\"Id\":\"1\",\"Name\":\"Initial\", \"TypeDefinition\": \"UnknownTrackedItem\"}",
                    FinalFileContent = "{\"Id\":\"1\",\"Name\":\"Final\", \"TypeDefinition\": \"UnknownTrackedItem\"}"
                }
            };

            _mockGitRepo.Setup(x => x.GetDiff(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(diffs);

            // Act
            var result = await _gitTrackingService.GetTrackedItemDiffs(unknownType, Guid.NewGuid().ToString());

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.IsType<TrackedItem>(result[0].Initial);
            Assert.IsType<TrackedItem>(result[0].Final);
            Assert.Equal("Initial", result[0].Initial.Name);
            Assert.Equal("Final", result[0].Final.Name);
        }

        [Fact]
        public async Task Test_ResetFileChanges_With_Added_ChangeKind_Calls_DeleteFiles_And_PerformDelete()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = "test-id",
                Name = "Test Blog Post"
            };

            var diff = new TrackedItemDiff
            {
                Final = blogPost,
                TrackedItemGitDiff = new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Added
                },
                ValueProviderDiffs = new List<GitDiff>()
            };

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/blog-posts/test-blog-post.json");

            _mockFileProvider.Setup(x => x.DeleteFiles(It.IsAny<TrackedItem[]>()))
                .Returns(Task.FromResult(true));

            _mockDeleteOperation.Setup(x => x.IsMatch(typeof(BlogPost)))
                .Returns(true);

            // Act
            await _gitTrackingService.ResetFileChanges(diff);

            // Assert
            _mockFileProvider.Verify(x => x.DeleteFileFromRelativePath(It.IsAny<string>()), Times.Once);
            _mockDeleteOperation.Verify(x => x.Delete(blogPost), Times.Once);
            _mockGitRepo.Verify(x => x.ResetFileChanges(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Test_ResetFileChanges_With_Added_ChangeKind_And_ValueProviderDiffs_Calls_DeleteFile()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = "test-id",
                Name = "Test Blog Post"
            };

            var valueProviderDiff = new GitDiff
            {
                Path = "/blog-posts/test-blog-post/body.md",
                ChangeKind = ChangeKind.Added
            };

            var diff = new TrackedItemDiff
            {
                Final = blogPost,
                TrackedItemGitDiff = new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Added
                },
                ValueProviderDiffs = new List<GitDiff> { valueProviderDiff }
            };

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/blog-posts/test-blog-post.json");

            _mockPathProvider.Setup(x => x.GetTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/local/blog-posts/test-blog-post");

            _mockFileProvider.Setup(x => x.DeleteFiles(It.IsAny<TrackedItem[]>()))
                .Returns(Task.FromResult(true));

            _mockFileProvider.Setup(x => x.DeleteFile(It.IsAny<string>()))
                .Returns(Task.FromResult(true));

            _mockDeleteOperation.Setup(x => x.IsMatch(typeof(BlogPost)))
                .Returns(true);

            // Act
            await _gitTrackingService.ResetFileChanges(diff);

            // Assert
            _mockFileProvider.Verify(x => x.DeleteFileFromRelativePath(It.IsAny<string>()), Times.AtLeastOnce);
            _mockDeleteOperation.Verify(x => x.Delete(blogPost), Times.Once);
        }

        [Fact]
        public async Task Test_ResetFileChanges_With_Modified_ChangeKind_Calls_GitRepo_ResetFileChanges()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = "test-id",
                Name = "Test Blog Post"
            };

            var diff = new TrackedItemDiff
            {
                Initial = blogPost,
                Final = blogPost,
                TrackedItemGitDiff = new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Modified
                },
                ValueProviderDiffs = new List<GitDiff>()
            };

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/blog-posts/test-blog-post.json");

            _mockGitRepo.Setup(x => x.ResetFileChanges(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockUpdateOperation.Setup(x => x.IsMatch(typeof(BlogPost)))
                .Returns(true);

            // Act
            await _gitTrackingService.ResetFileChanges(diff);

            // Assert
            _mockGitRepo.Verify(x => x.ResetFileChanges("/blog-posts/test-blog-post.json", null), Times.Once);
            _mockUpdateOperation.Verify(x => x.Update(blogPost), Times.Once);
            _mockFileProvider.Verify(x => x.DeleteFiles(It.IsAny<TrackedItem[]>()), Times.Never);
        }

        [Fact]
        public async Task Test_ResetFileChanges_With_Deleted_ChangeKind_Calls_PerformCreate()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = "test-id",
                Name = "Test Blog Post"
            };

            var diff = new TrackedItemDiff
            {
                Initial = blogPost,
                TrackedItemGitDiff = new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Deleted
                },
                ValueProviderDiffs = new List<GitDiff>()
            };

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/blog-posts/test-blog-post.json");

            _mockGitRepo.Setup(x => x.ResetFileChanges(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockCreateOperation.Setup(x => x.IsMatch(typeof(BlogPost)))
                .Returns(true);

            // Act
            await _gitTrackingService.ResetFileChanges(diff);

            // Assert
            _mockGitRepo.Verify(x => x.ResetFileChanges("/blog-posts/test-blog-post.json", null), Times.Once);
            _mockCreateOperation.Verify(x => x.Create(blogPost), Times.Once);
        }

        [Fact]
        public async Task Test_ResetFileChanges_Uses_Initial_When_Final_Is_Null()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = "test-id",
                Name = "Test Blog Post"
            };

            var diff = new TrackedItemDiff
            {
                Initial = blogPost,
                Final = null,
                TrackedItemGitDiff = new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Deleted
                },
                ValueProviderDiffs = new List<GitDiff>()
            };

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/blog-posts/test-blog-post.json");

            _mockGitRepo.Setup(x => x.ResetFileChanges(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockCreateOperation.Setup(x => x.IsMatch(typeof(BlogPost)))
                .Returns(true);

            // Act
            await _gitTrackingService.ResetFileChanges(diff);
            _mockCreateOperation.Verify(x => x.Create(blogPost), Times.Once);
        }

        [Fact]
        public async Task Test_ResetFileChanges_With_Mixed_ValueProviderDiffs()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = "test-id",
                Name = "Test Blog Post"
            };

            var addedValueProviderDiff = new GitDiff
            {
                Path = "/blog-posts/test-blog-post/body.md",
                ChangeKind = ChangeKind.Added
            };

            var modifiedValueProviderDiff = new GitDiff
            {
                Path = "/blog-posts/test-blog-post/summary.md",
                ChangeKind = ChangeKind.Modified
            };

            var diff = new TrackedItemDiff
            {
                Final = blogPost,
                TrackedItemGitDiff = new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Added
                },
                ValueProviderDiffs = new List<GitDiff> { addedValueProviderDiff, modifiedValueProviderDiff }
            };

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/blog-posts/test-blog-post.json");

            _mockPathProvider.Setup(x => x.GetTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns("/local/blog-posts/test-blog-post");

            _mockFileProvider.Setup(x => x.DeleteFiles(It.IsAny<TrackedItem[]>()))
                .Returns(Task.FromResult(true));

            _mockFileProvider.Setup(x => x.DeleteFile(It.IsAny<string>()))
                .Returns(Task.FromResult(true));

            _mockGitRepo.Setup(x => x.ResetFileChanges(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(true);

            _mockDeleteOperation.Setup(x => x.IsMatch(typeof(BlogPost)))
                .Returns(true);

            // Act
            await _gitTrackingService.ResetFileChanges(diff);

            // Assert
            _mockFileProvider.Verify(x => x.DeleteFileFromRelativePath(It.IsAny<string>()), Times.AtLeastOnce);
            _mockGitRepo.Verify(x => x.ResetFileChanges("/blog-posts/test-blog-post/summary.md", null), Times.Once);
            _mockDeleteOperation.Verify(x => x.Delete(blogPost), Times.Once);
        }

        [Fact]
        public async Task Test_Commit_Returns_Null_When_GitRepo_Commit_Returns_Empty_String()
        {
            // Arrange
            var diffs = new List<GitDiff>
            {
                new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Modified,
                    FinalFileContent = "{\"Id\":\"1\",\"Name\":\"Test Blog Post\",\"TypeDefinition\":\"BlogPost\"}"
                }
            };

            _mockGitRepo.Setup(x => x.GetStagedItems())
                .Returns(new List<string> { "/blog-posts/test-blog-post.json" });

            _mockGitRepo.Setup(x => x.Commit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(string.Empty); // Returns empty string to simulate failed commit

            _mockFileProvider.Setup(x => x.GetTrackedItemJsonForPath(It.IsAny<string>()))
                .Returns("{\"Id\":\"1\",\"Name\":\"Test Blog Post\",\"TypeDefinition\":\"BlogPost\"}");

            // Act
            var result = await _gitTrackingService.Commit("Test commit message", "test@example.com", "testuser");

            // Assert
            Assert.Null(result);
            _mockGitRepo.Verify(x => x.Commit("Test commit message", "test@example.com", "testuser"), Times.Once);
            _mockFileProvider.Verify(x => x.GetTrackedItemJsonForPath(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Test_Commit_Returns_Null_When_GitRepo_Commit_Returns_Null()
        {
            // Arrange
            var diffs = new List<GitDiff>
            {
                new GitDiff
                {
                    Path = "/blog-posts/test-blog-post.json",
                    ChangeKind = ChangeKind.Modified,
                    FinalFileContent = "{\"Id\":\"1\",\"Name\":\"Test Blog Post\",\"TypeDefinition\":\"BlogPost\"}"
                }
            };

            _mockGitRepo.Setup(x => x.GetStagedItems())
                .Returns(new List<string> { "/blog-posts/test-blog-post.json" });

            _mockGitRepo.Setup(x => x.Commit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string)null); // Returns null to simulate failed commit

            _mockFileProvider.Setup(x => x.GetTrackedItemJsonForPath(It.IsAny<string>()))
                .Returns("{\"Id\":\"1\",\"Name\":\"Test Blog Post\",\"TypeDefinition\":\"BlogPost\"}");

            // Act
            var result = await _gitTrackingService.Commit("Test commit message", "test@example.com", "testuser");

            // Assert
            Assert.Null(result);
            _mockGitRepo.Verify(x => x.Commit("Test commit message", "test@example.com", "testuser"), Times.Once);
            _mockFileProvider.Verify(x => x.GetTrackedItemJsonForPath(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Test_GetTrackedItemDiffs_With_Multiple_TrackedTypes()
        {
            // Arrange
            var trackedTypes = new List<Type> { typeof(BlogPost), typeof(Models.Tag) };

            var blogPostGuid = Guid.NewGuid();
            var tagGuid = Guid.NewGuid();
            var diffs = new List<GitDiff>
            {
                new GitDiff
                {
                    Path = $"/blog-posts/{blogPostGuid}.json",
                    ChangeKind = ChangeKind.Modified,
                    InitialFileContent = "{\"Id\":\"1\",\"Name\":\"Initial Blog Post\",\"TypeDefinition\":\"BlogPost\"}",
                    FinalFileContent = "{\"Id\":\"1\",\"Name\":\"Final Blog Post\",\"TypeDefinition\":\"BlogPost\"}"
                },
                new GitDiff
                {
                    Path = $"/tags/{tagGuid}.json", 
                    ChangeKind = ChangeKind.Added,
                    FinalFileContent = "{\"Id\":\"2\",\"Name\":\"New Tag\",\"TypeDefinition\":\"Tag\"}"
                }
            };

            _mockGitRepo.Setup(x => x.GetDiff(It.IsAny<IList<string>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(diffs);

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), It.IsAny<TrackedItem>()))
                .Returns("/blog-posts/test-blog-post.json");

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(Models.Tag), It.IsAny<TrackedItem>()))
                .Returns("/tags/test-tag.json");

            // Act
            var result = await _gitTrackingService.GetTrackedItemDiffs(trackedTypes, "commit1", "commit2");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            
            var blogPostDiff = result.FirstOrDefault(x => x.TrackedItemGitDiff.Path == $"/blog-posts/{blogPostGuid}.json");
            Assert.NotNull(blogPostDiff);
            Assert.Equal("Initial Blog Post", blogPostDiff.Initial.Name);
            Assert.Equal("Final Blog Post", blogPostDiff.Final.Name);
            Assert.Equal(ChangeKind.Modified, blogPostDiff.TrackedItemGitDiff.ChangeKind);

            var tagDiff = result.FirstOrDefault(x => x.TrackedItemGitDiff.Path == $"/tags/{tagGuid}.json");
            Assert.NotNull(tagDiff);
            Assert.Null(tagDiff.Initial);
            Assert.Equal("New Tag", tagDiff.Final.Name);
            Assert.Equal(ChangeKind.Added, tagDiff.TrackedItemGitDiff.ChangeKind);

            _mockGitRepo.Verify(x => x.GetDiff(It.IsAny<IList<string>>(), "commit1", "commit2"), Times.Once);
        }



        // Helper class for testing - a TrackedItem type not in GitConfig.TrackedTypes
        public class UnknownTrackedItem : TrackedItem
        {
            public string CustomProperty { get; set; }
        }

        [Fact]
        public async Task Test_GetTrackedItem_Returns_TrackedItem_From_FileProvider()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Blog Post"
            };

            var relativePath = $"/blog-post/{blogPost.Id}.json";
            var expectedJson = "{\"Id\":\"test-id\",\"Name\":\"Test Blog Post\",\"TypeDefinition\":\"BlogPost\"}";

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns(relativePath);

            _mockFileProvider.Setup(x => x.GetFile(relativePath))
                .Returns(expectedJson);

            // Act
            var result = await _gitTrackingService.GetTrackedItem(typeof(BlogPost), blogPost);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test-id", result.Id);
            Assert.Equal("Test Blog Post", result.Name);
            _mockPathProvider.Verify(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost), Times.Once);
            _mockFileProvider.Verify(x => x.GetFile(relativePath), Times.Once);
        }

        [Fact]
        public async Task Test_GetTrackedItem_Returns_Null_When_FileProvider_Returns_Null()
        {
            // Arrange
            var blogPost = new BlogPost
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Blog Post"
            };

            var relativePath = $"/blog-post/{blogPost.Id}.json";

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost))
                .Returns(relativePath);

            _mockFileProvider.Setup(x => x.GetFile(relativePath))
                .Returns((string)null);

            // Act
            var result = await _gitTrackingService.GetTrackedItem(typeof(BlogPost), blogPost);

            // Assert
            Assert.Null(result);
            _mockPathProvider.Verify(x => x.GetRelativeTrackedItemPath(typeof(BlogPost), blogPost), Times.Once);
            _mockFileProvider.Verify(x => x.GetFile(relativePath), Times.Once);
        }

        [Fact]
        public async Task Test_GetTrackedItem_Returns_Generic_TrackedItem_For_Unknown_Type()
        {
            // Arrange
            var unknownItem = new UnknownTrackedItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Unknown Item",
                CustomProperty = "Custom Value"
            };

            var expectedJson = "{\"Id\":\"unknown-id\",\"Name\":\"Unknown Item\",\"CustomProperty\":\"Custom Value\",\"TypeDefinition\":\"UnknownTrackedItem\"}";
            var relativePath = $"/unknown/{unknownItem.Id}.json";

            _mockPathProvider.Setup(x => x.GetRelativeTrackedItemPath(typeof(UnknownTrackedItem), unknownItem))
                .Returns(relativePath);

            _mockFileProvider.Setup(x => x.GetFile(relativePath))
                .Returns(expectedJson);

            // Act
            var result = await _gitTrackingService.GetTrackedItem(typeof(UnknownTrackedItem), unknownItem);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("unknown-id", result.Id);
            Assert.Equal("Unknown Item", result.Name);
            _mockPathProvider.Verify(x => x.GetRelativeTrackedItemPath(typeof(UnknownTrackedItem), unknownItem), Times.Once);
            _mockFileProvider.Verify(x => x.GetFile(relativePath), Times.Once);
        }
    }
}