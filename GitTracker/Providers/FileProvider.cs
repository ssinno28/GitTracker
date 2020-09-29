using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GitTracker.Providers
{
    public class FileProvider : IFileProvider
    {
        private readonly GitConfig _gitConfig;
        private readonly IPathProvider _pathProvider;
        private readonly ContentContractResolver _contentContractResolver;
        private readonly ILogger<FileProvider> _logger;

        public FileProvider(
            GitConfig gitConfig, 
            IPathProvider pathProvider, 
            ContentContractResolver contentContractResolver,
            ILoggerFactory loggerFactory)
        {
            _gitConfig = gitConfig;
            _pathProvider = pathProvider;
            _contentContractResolver = contentContractResolver;
            _logger = loggerFactory.CreateLogger<FileProvider>();
        }

        public IList<string> GetFiles(IList<Type> contentTypes)
        {
            IList<string> filePaths = new List<string>();

            foreach (var contentType in contentTypes)
            {
                string contentTypeFolderPath = $"{_gitConfig.LocalPath}\\{contentType.Name}";
                if (!Directory.Exists(contentTypeFolderPath)) continue;

                var paths = 
                    Directory.GetFiles(contentTypeFolderPath, "*.json", SearchOption.AllDirectories)
                        .ToList();

                paths.ForEach(x => filePaths.Add(x));
            }

            return filePaths.Select(File.ReadAllText).ToList();
        }

        public async Task<bool> DeleteFiles(params TrackedItem[] trackedItems)
        {
            return await Task.Run(() =>
            {
                try
                {
                    foreach (var contentItem in trackedItems)
                    {
                        var currentContentItemPath = _pathProvider.GetTrackedItemPath(contentItem.GetType(), contentItem);
                        if (Directory.Exists(currentContentItemPath))
                        {
                            Directory.Delete(currentContentItemPath, true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not delete files {0}, error {1}", trackedItems.SelectMany(x => x.Id), ex);
                    return false;
                }

                return true;
            });
        }

        public async Task<bool> UpsertFiles(params TrackedItem[] trackedItems)
        {
            return await Task.Run(() =>
            {
                try
                {
                    foreach (var trackedItem in trackedItems)
                    {
                        var contentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);

                        string fileContents =
                            JsonConvert.SerializeObject(trackedItem, Formatting.Indented, new JsonSerializerSettings
                            {
                                ContractResolver = _contentContractResolver
                            });

                        File.WriteAllText(Path.Combine(contentItemPath, $"{trackedItem.Id}.json"), fileContents);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not upsert files {0}, error {1}", trackedItems.SelectMany(x => x.Id), ex);
                    return false;
                }

                return true;
            });
        }

        public async Task<bool> MoveFile(string newName, TrackedItem trackedItem)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var currentContentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);
                    trackedItem.Name = newName;

                    var newContentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);
                    Directory.CreateDirectory(newContentItemPath);

                    foreach (var file in Directory.GetFiles(currentContentItemPath))
                    {
                        File.Move(file, Path.Combine(newContentItemPath, Path.GetFileName(file)));
                    }

                    Directory.Delete(currentContentItemPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not move content item {0}, error {1}", trackedItem.Id, ex);
                    return false;
                }

                return true;
            });
        }
    }
}