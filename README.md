# GitTracker

![CI](https://github.com/ssinno28/GitTracker/workflows/CI/badge.svg)

**NOT READY FOR PRODUCTION USE**

This is an experimental library that allows you to use git to version control your content. It is not ready for a production environment and there will most likely be API breaking changes in the future. 

## Getting Started

To start you will need to wire up the appropriate services in DI:

```c#
var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole())
                .AddGitTracking(LocalPath, "test", RemotePath, string.Empty, contentTypes);
```

The `AddGitTracking` extension method takes these arguments:

```c#
 public static IServiceCollection AddGitTracking(this IServiceCollection services, 
            string localPath,
            string token,
            string remotePath,
            string webhookSecret,
            IList<Type> trackedTypes)
```

The list of tracked types are POCO objects that inherit from `TrackedItem`. 

It is also very important to wire up your CRUD operations using the `ICreateOperation`, `IDeleteOperation` and `IUpdateOperation` interfaces. Whenever an item is created, updated or deleted in the local git repository, the appropriate CRUD operation is called to also apply that change to whatever database you are using. 

## Content Storage

The way it works is it creates a folder for each content type and then under that a folder for each content item:

![content-types](https://github.com/ssinno28/GitTracker/blob/master/readme-images/content-types.PNG)

And if we dig further down we can see that each one of our content items has a folder:

![content-items](https://github.com/ssinno28/GitTracker/blob/master/readme-images/content-items.PNG)

And if we go a little further we can see that most content items will have multple files, a json file that contains metadata for the content item and things like images or markdown files:

![content-item](https://github.com/ssinno28/GitTracker/blob/master/readme-images/content-item.PNG)

## Syncing Using GitHub WebHooks

You'll need to create a personal access token as documented here: https://docs.github.com/en/free-pro-team@latest/github/authenticating-to-github/creating-a-personal-access-token. The personal access token will allow the program to push and pull from the remote repo. Another property to note is the `webhookSecret`. The webhook secret allows you to secure your webhook and is used in conjunction with the IGitRepo.IsGithubPushAllowed method. 

``` c#
   public class ContentUpdateModule : NancyModule
    {
        private readonly IGitTrackingService _gitTrackingService;
        private readonly IGitRepo _gitRepo;
       
        public ContentUpdateModule(IGitTrackingService gitTrackingService, IGitRepo gitRepo) : base("/api")
        {
            _gitTrackingService = gitTrackingService;
            _gitRepo = gitRepo;

            Post("contentupdate", async (o, token) =>
            {
                var signatureWithPrefix = Request.Headers["X-Hub-Signature"].FirstOrDefault();
                using (var reader = new StreamReader(Request.Body))
                {
                    var txt = await reader.ReadToEndAsync();
                    if (!_gitRepo.IsGithubPushAllowed(txt, signatureWithPrefix))
                    {
                        return HttpStatusCode.Unauthorized;
                    }

                    // Using theirs will make sure that there are never any merge conflicts
                    _gitTrackingService.Sync("your_email", CheckoutFileConflictStrategy.Theirs);

                    return HttpStatusCode.Accepted;
                }
            });
        }
    }
```



## Staging and Commiting

When actually working with content you will always want to use the `IGitTrackingService`, but there may be times when you don't want to be so abstracted and the `IGitRepo` helps to interact directly with the git repository. A normal operation will look something like this:

```c#
string email = "john.doe@gmail.com";
var trackedBlogPost =
    await gitTrackingService.Create(new BlogPost()
                                    {
                                        Name = "My second blog post"
                                    });

gitTrackingService.Stage(trackedBlogPost);

// here we work directly with the git repo to make a commit
gitRepo.Commit("This is my commit", email);
await gitTrackingService.Publish(email);
```



## Value Providers

While the majority of the object is serialized into JSON, you can easily abstract fields out of the JSON and into separate files if it is easier to maintain (for instance a markdown file would be a good example). This is very important as merge conflicts could become pretty much impossible for files like this if the remain in the serialized JSON object. 

In order to do this you need to create a class that implements `IValueProvider`.

```c#
    public class MarkdownValueProvider : IValueProvider
    {
        private readonly IPathProvider _pathProvider;

        public MarkdownValueProvider(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public bool IsMatch(PropertyInfo propertyInfo)
        {
            return propertyInfo.GetCustomAttribute<MarkdownAttribute>() != null;
        }

        public async Task<object> GetValue(TrackedItem trackedItem, PropertyInfo propertyInfo)
        {
            var contentItemPath = _pathProvider.GetTrackedItemPath(trackedItem.GetType(), trackedItem);
            string filePath = Path.Combine(contentItemPath, $"{propertyInfo.Name.ToSentenceCase().MakeUrlFriendly()}.md");

            if (!File.Exists(filePath)) return string.Empty;

            return File.ReadAllText(filePath);
        }
    }
```

If you want to make sure that, value is always properly fetched from a separate file and not the json file then the `IsMatch` method needs to return true. In this instance we have a property that has a `[Markdown]` attribute assigned to it. 



## Viewing Diffs

There are several different ways you can get a diff (for instance a diff from the head of the repo or between two commits). 

```
// Gets diff from head
var diff = await gitTrackingService.GetTrackedItemDiffs();

// Gets diff for the most recent commit
var diff = await gitTrackingService.GetTrackedItemDiffs(gitRepo.GetCurrentCommitId());

// Gets diff between two commits
var diff = await gitTrackingService.GetTrackedItemDiffs(gitRepo.GetCurrentCommitId(), "second commit id");
```

 

## Getting Merge Conflicts

If the repository is currently in a state of conflict, then calling `gitTrackingService.GetTrackedItemConflicts()` will return a list of `TrackedItemConflict`. 



```c#
    public class TrackedItemConflict
    {
        public TrackedItem Ancestor { get; set; }
        public TrackedItem Theirs { get; set; }
        public TrackedItem Ours { get; set; }
        public IList<PropertyInfo> ChangedProperties { get; set; }
        public IList<ValueProviderConflict> ValueProviderConflicts { get; set; }
    }
```

This will return a list of all the properties that were changed as well as deserialize the tracked item for the base, local and remote into objects. It also returns a list of `ValueProviderConflicts`, which simply has the paths to the BASE, LOCAL and REMOTE version of the files so you can use an external merge tool to to resolve the conflict. 

