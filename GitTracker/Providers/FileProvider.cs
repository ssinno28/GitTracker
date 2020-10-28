﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using GitTracker.Helpers;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GitTracker.Providers
{
    public class FileProvider : IFileProvider
    {
        private readonly IPathProvider _pathProvider;
        private readonly ContentContractResolver _contentContractResolver;
        private readonly ILogger<FileProvider> _logger;
        private readonly ILocalPathFactory _localPathFactory;
        private readonly IFileSystem _fileSystem;

        public FileProvider(
            IPathProvider pathProvider,
            ContentContractResolver contentContractResolver,
            ILoggerFactory loggerFactory, 
            ILocalPathFactory localPathFactory, IFileSystem fileSystem)
        {
            _pathProvider = pathProvider;
            _contentContractResolver = contentContractResolver;
            _localPathFactory = localPathFactory;
            _fileSystem = fileSystem;
            _logger = loggerFactory.CreateLogger<FileProvider>();
        }

        public string GetFile(string path)
        {
            string absolutePath = Path.Combine(_localPathFactory.GetLocalPath(), path);
            return _fileSystem.File.ReadAllText(absolutePath);
        }
        
        public string GetTrackedItemJsonForPath(string path)
        {
            string absolutePath = Path.Combine(_localPathFactory.GetLocalPath(), path);
            string directory = Path.GetDirectoryName(absolutePath);

            var trackedItemPath =
                _fileSystem.Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
                    .Single(x => x.IsTrackedItemJson());

            return trackedItemPath;
        }

        public IList<string> GetFiles(IList<Type> contentTypes)
        {
            IList<string> filePaths = new List<string>();

            foreach (var contentType in contentTypes)
            {
                string contentTypeFolderPath = _pathProvider.GetTrackedItemPath(contentType);
                if (!_fileSystem.Directory.Exists(contentTypeFolderPath)) continue;

                var paths =
                    _fileSystem.Directory.GetFiles(contentTypeFolderPath, "*.json", SearchOption.AllDirectories)
                        .ToList();

                paths.ForEach(x =>
                {
                    if (x.IsTrackedItemJson())
                    {
                        filePaths.Add(x);
                    }
                });
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
                        if (_fileSystem.Directory.Exists(currentContentItemPath))
                        {
                            _fileSystem.Directory.Delete(currentContentItemPath, true);
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
                        if (!_fileSystem.Directory.Exists(contentItemPath))
                        {
                            _fileSystem.Directory.CreateDirectory(contentItemPath);
                        }

                        string fileContents =
                            JsonConvert.SerializeObject(trackedItem, Formatting.Indented, new JsonSerializerSettings
                            {
                                ContractResolver = _contentContractResolver
                            });

                        _fileSystem.File.WriteAllText(Path.Combine(contentItemPath, $"{trackedItem.Id}.json"), fileContents);
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
                var currentContentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);
                trackedItem.Name = newName;

                var newContentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);
                if (_fileSystem.Directory.Exists(newContentItemPath))
                {
                    throw new Exception($"The name {newName} already exists!");
                }

                _fileSystem.Directory.CreateDirectory(newContentItemPath);

                foreach (var file in _fileSystem.Directory.GetFiles(currentContentItemPath))
                {
                    File.Move(file, Path.Combine(newContentItemPath, Path.GetFileName(file)));
                }

                _fileSystem.Directory.Delete(currentContentItemPath);

                return true;
            });
        }
    }
}