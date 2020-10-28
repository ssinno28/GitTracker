using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Providers;
using GitTracker.Repositories;
using GitTracker.Serializer;
using GitTracker.Services;
using GitTracker.ValueProviders;
using Microsoft.Extensions.DependencyInjection;

namespace GitTracker.Helpers
{
    public static class ServiceCollectionHelpers
    {
        public static IServiceCollection AddGitTracking(this IServiceCollection services, 
            string token,
            string remotePath,
            string webhookSecret,
            IList<Type> trackedTypes)
        {
            var gitConfig = new GitConfig
            {
                Token = token,
                RemotePath = remotePath,
                WebhookSecret = webhookSecret,
                TrackedTypes = trackedTypes
            };

            services.AddSingleton(gitConfig);
            services.AddScoped<IGitRepo, GitRepo>();
            services.AddScoped<IPathProvider, PathProvider>();
            services.AddScoped<IFileProvider, FileProvider>();
            services.AddScoped<IGitTrackingService, GitTrackingService>();
            services.AddScoped<IFileSystem, FileSystem>();
            services.AddScoped<ContentContractResolver>();

            var assembly = Assembly.GetAssembly(typeof(MarkdownValueProvider));
            foreach (var exportedType in assembly.DefinedTypes)
            {
                if (exportedType.ImplementedInterfaces.Contains(typeof(IValueProvider)))
                {
                    services.AddScoped(typeof(IValueProvider), exportedType);
                }
            }

            return services;
        }
    }
}