# GitTracker

![CI](https://github.com/ssinno28/GitTracker/workflows/CI/badge.svg)

## Getting Started

To start you will need to wire up the appropriate services in DI:

```c#
var contentTypes = new List<Type> { typeof(BlogPost), typeof(Tag), typeof(Category) };
var serviceCollection = new ServiceCollection()
                .AddLogging(x => x.AddConsole())
                .AddGitTracking("test", RemotePath, string.Empty, contentTypes);
```

The `AddGitTracking` extension method takes these arguments:

```c#
 public static IServiceCollection AddGitTracking(this IServiceCollection services, 
            string token,
            string remotePath,
            string webhookSecret,
            IList<Type> trackedTypes)
```

The list of tracked types are POCO objects that inherit from `TrackedItem`. 

You'll want to create an instance of `ILocalPathFactory` as well, this will return your local path for the git repo. Here is an example:

```c#
    public class LocalPathFactory : ILocalPathFactory
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LocalPathFactory(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetLocalPath()
        {
            string userId = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Name).Value;
            return $"fake-path/{userId}";
        }
    }
``` 



You'll most likely want to have a different path for every user that uses your system.

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
                    await _gitTrackingService.Sync("your_email", CheckoutFileConflictStrategy.Theirs);

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



## Changing Branches, Pulling and Pushing

When performing any of these operations you will always want to use the IGitTrackingService. The reason being is that it will make sure your data store is in sync with the git repo, whereas IGitRepo methods only affect the git repository. 

```c#
await _gitTrackingService.SwitchBranch("master");
await _gitTrackingService.CreateBranch("test-branch");
await _gitTrackingService.Publish(email);
await _gitTrackingService.Sync(email);
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

```c#
// Gets diff from head
var diff = await gitTrackingService.GetTrackedItemDiffs();

// Gets diff for the most recent commit
var diff = await gitTrackingService.GetTrackedItemDiffs(gitRepo.GetCurrentCommitId());

// Gets diff between two commits
var diff = await gitTrackingService.GetTrackedItemDiffs(gitRepo.GetCurrentCommitId(), "second commit id");
```

 

## Merge Conflicts

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

In order to solve the merge conflict you then just have to update the tracked item and stage the changes.

```c#
    bool failedMerge = await GitTrackingService.MergeBranch("test-branch", Email);

    var conflicts = await GitTrackingService.GetTrackedItemConflicts();            

    // take ours and merge
    var conflict = conflicts.First();
    await gitTrackingService.Update(conflict.Ours);
    gitTrackingService.Stage(conflict.Ours);
    gitRepo.Commit("Fixing Merge Conflict", Email);

    bool successfulMerge = await gitTrackingService.MergeBranch("test-branch", Email);
```



