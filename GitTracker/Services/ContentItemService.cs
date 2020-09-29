using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Serializer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using IValueProvider = GitTracker.Interfaces.IValueProvider;

namespace GitTracker.Services
{
    public class ContentItemService : IContentItemService
    {
        private readonly ContentContractResolver _contentContractResolver;
        private readonly IEnumerable<IValueProvider> _valueProviders;
        private readonly IFileProvider _fileProvider;
        private readonly IPathProvider _pathProvider;

        public ContentItemService(
            ContentContractResolver contentContractResolver,
            IEnumerable<IValueProvider> valueProviders,
            IFileProvider fileProvider,
            IPathProvider pathProvider)
        {
            _contentContractResolver = contentContractResolver;
            _valueProviders = valueProviders;
            _fileProvider = fileProvider;
            _pathProvider = pathProvider;
        }

        public async Task Sync()
        {
            // get current commit id

            // pull

            // get new commit id

            // get diff from both
        }

        public async Task Add(string entity, IList<Type> contentTypes)
        {
            var contentItem = DeserializeContentItem(entity, contentTypes);
            await Add(contentItem);
        }

        private async Task SetNonJsonValues(TrackedItem contentItem)
        {
            foreach (var propertyInfo in contentItem.GetType().GetProperties())
            {
                var valueProvider =
                    _valueProviders.FirstOrDefault(x => x.IsMatch(propertyInfo));

                if (valueProvider != null)
                {
                    propertyInfo.SetValue(contentItem, await valueProvider.GetValue(contentItem, propertyInfo));
                }
            }
        }

        public async Task Add(TrackedItem contentItem)
        {
            await SetNonJsonValues(contentItem);

            contentItem.Id = Guid.NewGuid().ToString();
            contentItem.CreatedDate = DateTimeOffset.Now;

            await _fileProvider.UpsertFiles(contentItem);
        }

        public async Task<bool> Update(TrackedItem contentItem)
        {
            await SetNonJsonValues(contentItem);

            contentItem.ModifiedDate = DateTimeOffset.Now;
            await _fileProvider.UpsertFiles(contentItem);
            return true;
        }

        public async Task<bool> ChangeName(string newName, TrackedItem contentItem)
        {
            contentItem.PreviousPaths.Add(_pathProvider.GetRelativeTrackedItemPath(contentItem.GetType(), contentItem));
            await _fileProvider.MoveFile(newName, contentItem);
            await _fileProvider.UpsertFiles(contentItem);

            return true;
        }

        public async Task<TrackedItem> CreateDraft(string name, Type contentType, TrackedItem contentItem = null)
        {
            if (contentItem == null)
            {
                contentItem = (TrackedItem)Activator.CreateInstance(contentType);
            }

            contentItem.Name = name;
            contentItem.Id = Guid.NewGuid().ToString();
            contentItem.CreatedDate = DateTimeOffset.Now;

            await _fileProvider.UpsertFiles(contentItem);

            return contentItem;
        }

        public async Task Delete(TrackedItem contentItem)
        {
            await _fileProvider.DeleteFiles(contentItem);
        }

        private Type GetContentType(string entity, IList<Type> contentTypes)
        {
            string typeDefinition = JObject.Parse(entity).GetValue("TypeDefinition").Value<string>();
            return contentTypes.First(x => x.Name.Equals(typeDefinition));
        }

        private TrackedItem DeserializeContentItem(string document, IList<Type> contentTypes)
        {
            var contentType = GetContentType(document, contentTypes);
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = _contentContractResolver
            };

            var contentItem = (TrackedItem)JsonConvert.DeserializeObject(document, contentType, serializerSettings);

            return contentItem;
        }
    }
}