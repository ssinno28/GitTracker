﻿using System;
using System.Linq;
using System.Reflection;
using GitTracker.Interfaces;
using GitTracker.Models;
using GitTracker.Providers;
using GitTracker.Repositories;
using GitTracker.Serializer;
using GitTracker.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitTracker.Helpers
{
    public static class ServiceCollectionHelpers
    {
        public static IServiceCollection AddGitTracking(this IServiceCollection services, 
            string localPath,
            string token,
            string remotePath,
            string webhookSecret)
        {
            var gitConfig = new GitConfig
            {
                Token = token,
                RemotePath = remotePath,
                LocalPath = localPath,
                WebhookSecret = webhookSecret
            };

            services.AddSingleton(gitConfig);
            services.AddScoped<IGitRepo, GitRepo>();
            services.AddScoped<IPathProvider, PathProvider>();
            services.AddScoped<IFileProvider, FileProvider>();
            services.AddScoped<IContentItemService, ContentItemService>();
            services.AddScoped<ContentContractResolver>();

            var assembly = Assembly.GetAssembly(typeof(MarkdownValueProvider));
            foreach (var exportedType in assembly.DefinedTypes)
            {
                if (exportedType.ImplementedInterfaces.Contains(typeof(IValueProvider)))
                {
                    services.AddScoped(typeof(IValueProvider), exportedType);
                }
                
                if (exportedType.ImplementedInterfaces.Contains(typeof(ICreateOperation)))
                {
                    services.AddScoped(typeof(ICreateOperation), exportedType);
                }    
                
                if (exportedType.ImplementedInterfaces.Contains(typeof(IUpdateOperation)))
                {
                    services.AddScoped(typeof(IUpdateOperation), exportedType);
                } 
                
                if (exportedType.ImplementedInterfaces.Contains(typeof(IDeleteOperation)))
                {
                    services.AddScoped(typeof(IDeleteOperation), exportedType);
                }
            }

            return services;
        }
    }
}