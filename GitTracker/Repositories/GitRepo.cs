using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitTracker.Dictionary;
using GitTracker.Interfaces;
using GitTracker.Models;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Logging;

namespace GitTracker.Repositories
{
    public class GitRepo : IGitRepo
    {
        private const string Sha1Prefix = "sha1=";

        private readonly GitConfig _gitConfig;
        private readonly ILogger<GitRepo> _logger;
        private readonly ILocalPathFactory _localPathFactory;

        public GitRepo(GitConfig gitConfig, ILoggerFactory loggerFactory, ILocalPathFactory localPathFactory)
        {
            _gitConfig = gitConfig;
            _localPathFactory = localPathFactory;
            _logger = loggerFactory.CreateLogger<GitRepo>();
        }

        private Repository LocalRepo => new Repository(_localPathFactory.GetLocalPath());
        private Repository RemoteRepo => new Repository(_gitConfig.RemotePath);

        public bool Pull(string email, CheckoutFileConflictStrategy strategy, string username = null)
        {
            if (string.IsNullOrEmpty(_gitConfig.Token))
            {
                _logger.LogWarning($"No token specified for remote ${_gitConfig.RemotePath}, assuming local folder bare repo is used.");
            }

            string localPath = _localPathFactory.GetLocalPath();
            if (!Directory.Exists(localPath) || !Repository.IsValid(localPath))
            {
                Repository.Init(localPath);
            }

            using (var repo = LocalRepo)
            {
                if (repo.Network.Remotes["origin"] == null && string.IsNullOrEmpty(_gitConfig.RemotePath))
                {
                    _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because no origin is set.");
                    return false;
                }

                SetupAndTrack(repo, username ?? email);

                FetchOptions fetchOptions = new FetchOptions();
                if (!string.IsNullOrEmpty(_gitConfig.Token))
                {
                    CredentialsHandler credentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = username,
                            Password = _gitConfig.Token
                        };

                    fetchOptions =
                        new FetchOptions
                        {
                            CredentialsProvider = credentialsProvider,
                        };
                }

                // Credential information to fetch
                PullOptions options = new PullOptions
                {
                    FetchOptions = fetchOptions,
                    MergeOptions = new MergeOptions
                    {
                        FastForwardStrategy = FastForwardStrategy.Default,
                        FileConflictStrategy = strategy
                    }
                };

                // User information to create a merge commit
                var signature = new Signature(
                    new Identity(username ?? email, email), DateTimeOffset.Now);

                // Pull
                Commands.Pull(repo, signature, options);

                // if merge strategy is theirs or ours then we stage and commit for them
                if (repo.Index.Conflicts.Any() && (strategy == CheckoutFileConflictStrategy.Theirs || strategy == CheckoutFileConflictStrategy.Ours))
                {
                    string commitMsg = GetMergeCommitMessage();
                    foreach (var indexConflict in repo.Index.Conflicts)
                    {
                        Commands.Stage(repo,
                            strategy == CheckoutFileConflictStrategy.Theirs
                                ? indexConflict.Theirs.Path
                                : indexConflict.Ours.Path);
                    }

                    Commit(commitMsg, email);
                }
                else if (repo.Index.Conflicts.Any())
                {
                    return false;
                }
            }

            return true;
        }

        public string GetFileFromCommit(string commitId, string path)
        {
            string fileContents = null;
            using (var repo = LocalRepo)
            {
                var commits = repo.Branches.SelectMany(x => x.Commits).ToList();
                commits.AddRange(repo.Stashes.Select(x => x.Index));
                var ourCommit =
                    commits.First(x => x.Id.ToString().Equals(commitId));
                try
                {
                    var ourBlob = ourCommit[path].Target as Blob;
                    using (var content = new StreamReader(ourBlob.GetContentStream(), Encoding.UTF8))
                    {
                        fileContents = content.ReadToEnd();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInformation(ex, $"file does not exist for commit id ${commitId} and path ${path}");
                }
            }

            return fileContents;
        }

        public RevertStatus RevertCommit(string commitId, string email, string userName = null)
        {
            RevertStatus revertStatus;
            using (var repo = LocalRepo)
            {
                var commit = repo.Commits.First(x => x.Id.ToString().Equals(commitId));

                var signature = new Signature(
                    new Identity(userName ?? email, email), DateTimeOffset.Now);

                var revertResult = repo.Revert(commit, signature, new RevertOptions
                {
                    FileConflictStrategy = CheckoutFileConflictStrategy.Normal
                });

                revertStatus = revertResult.Status;
            }

            return revertStatus;
        }

        public GitMergeCommits GetDiff3Files(string localPath, string remotePath, string basePath = null)
        {
            var gitMergeCommits = new GitMergeCommits();

            using (var repo = LocalRepo)
            {
                var ourCommitId = GetCurrentCommitId();
                var theirCommitId = repo.Branches[$"origin/{GetCurrentBranch()}"].Tip.Id.ToString();
                var baseCommitId = GetMergeBase(ourCommitId, theirCommitId);

                var ourCommit =
                    repo.Commits.First(x => x.Id.ToString().Equals(ourCommitId));

                var ourBlob = ourCommit[localPath].Target as Blob;
                using (var content = new StreamReader(ourBlob.GetContentStream(), Encoding.UTF8))
                {
                    gitMergeCommits.OurFile = content.ReadToEnd();
                }

                var theirCommit = repo.Branches[$"origin/{GetCurrentBranch()}"].Tip;
                var theirBlob = theirCommit[remotePath].Target as Blob;
                using (var content = new StreamReader(theirBlob.GetContentStream(), Encoding.UTF8))
                {
                    gitMergeCommits.TheirFile = content.ReadToEnd();
                }

                if (!string.IsNullOrEmpty(basePath))
                {
                    var baseCommit =
                        repo.Commits.First(x => x.Id.ToString().Equals(baseCommitId));
                    var baseBlob = baseCommit[basePath].Target as Blob;
                    using (var content = new StreamReader(baseBlob.GetContentStream(), Encoding.UTF8))
                    {
                        gitMergeCommits.BaseFile = content.ReadToEnd();
                    }
                }
            }

            return gitMergeCommits;
        }

        private string GetMergeBase(string a, string b)
        {
            using (var repo = LocalRepo)
            {
                var aCommit = repo.Lookup<Commit>(a);
                var bCommit = repo.Lookup<Commit>(b);
                if (aCommit == null || bCommit == null)
                    return null;
                var baseCommit = repo.ObjectDatabase.FindMergeBase(aCommit, bCommit);
                return baseCommit != null ? baseCommit.Sha : null;
            }
        }

        public bool MergeBranch(string branchName, string email, CheckoutFileConflictStrategy strategy, string userName = null)
        {
            MergeStatus status;
            using (var repo = LocalRepo)
            {
                var branchToMerge = repo.Branches.First(x => x.FriendlyName.Equals(branchName));

                var signature = new Signature(
                    new Identity(userName ?? email, email), DateTimeOffset.Now);

                var mergeOptions = new MergeOptions
                {
                    FastForwardStrategy = FastForwardStrategy.Default,
                    FileConflictStrategy = strategy
                };

                var mergeResult = repo.Merge(branchToMerge, signature, mergeOptions);
                status = mergeResult.Status;

                // if merge strategy is theirs or ours then we stage and commit for them
                if (repo.Index.Conflicts.Any() && (strategy == CheckoutFileConflictStrategy.Theirs || strategy == CheckoutFileConflictStrategy.Ours))
                {
                    string commitMsg = GetMergeCommitMessage();
                    foreach (var indexConflict in repo.Index.Conflicts)
                    {
                        Commands.Stage(repo,
                            strategy == CheckoutFileConflictStrategy.Theirs
                                ? indexConflict.Theirs.Path
                                : indexConflict.Ours.Path);
                    }

                    Commit(commitMsg, email);
                }
                else if (repo.Index.Conflicts.Any())
                {
                    return false;
                }
            }

            return status != MergeStatus.Conflicts;
        }

        public IList<Conflict> GetMergeConflicts()
        {
            IList<Conflict> conflicts;
            using (var repo = LocalRepo)
            {
                conflicts = repo.Index.Conflicts.ToList();
            }

            return conflicts;
        }

        public string GetMergeCommitMessage()
        {
            using var repo = LocalRepo;
            if (!repo.Index.Conflicts.Any()) return string.Empty;

            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"Merge branch {GetCurrentBranch()} of {_gitConfig.RemotePath}");
            stringBuilder.AppendLine(string.Empty);
            stringBuilder.AppendLine("Conflicts: ");

            foreach (var conflict in repo.Index.Conflicts)
            {
                stringBuilder.AppendLine($"        {conflict.Theirs.Path}");
            }

            return stringBuilder.ToString();
        }

        public bool Push(string email, string username = null)
        {
            if (string.IsNullOrEmpty(_gitConfig.Token))
            {
                _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because there is no token.");
                return false;
            }

            using (var repo = LocalRepo)
            {
                if (repo.Network.Remotes["origin"] == null && string.IsNullOrEmpty(_gitConfig.RemotePath))
                {
                    _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because no origin is set.");
                    return false;
                }

                SetupAndTrack(repo, username ?? email);

                

                // Credential information to fetch
                PushOptions options = new PushOptions();
                if (!string.IsNullOrEmpty(_gitConfig.Token))
                {
                    CredentialsHandler credentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = username ?? email,
                            Password = _gitConfig.Token
                        };

                    options = new PushOptions
                    {
                        CredentialsProvider = credentialsProvider
                    };
                }

                // push
                var currentBranch = repo.Branches.FirstOrDefault(x => x.IsCurrentRepositoryHead);
                repo.Network.Push(currentBranch, options);
            }

            return true;
        }



        private void SetupAndTrack(Repository repo, string userName)
        {
            if (repo.Network.Remotes["origin"] == null)
            {
                repo.Network.Remotes.Add("origin", _gitConfig.RemotePath);
            }

            Remote remote = repo.Network.Remotes["origin"];
            var currentBranch = repo.Branches.FirstOrDefault(x => x.IsCurrentRepositoryHead);

            FetchOptions fetchOptions = new FetchOptions();
            if (!string.IsNullOrEmpty(_gitConfig.Token))
            {
                CredentialsHandler credentialsProvider = (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials
                    {
                        Username = userName,
                        Password = _gitConfig.Token
                    };

                fetchOptions =
                    new FetchOptions
                    {
                        CredentialsProvider = credentialsProvider,
                    };
            }

            if (currentBranch == null)
            {

                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, string.Empty);

                var trackedMasterBranch = repo.Branches["origin/master"];
                currentBranch = repo.CreateBranch("master", trackedMasterBranch.Tip);
                Commands.Checkout(repo, currentBranch);
            }

            if (!currentBranch.IsTracking)
            {
                repo.Branches.Update(currentBranch,
                    b => b.Remote = remote.Name,
                    b => b.UpstreamBranch = currentBranch.CanonicalName);
            }
        }

        public bool IsGithubPushAllowed(string payload, string signatureWithPrefix)
        {
            if (signatureWithPrefix.StartsWith(Sha1Prefix, StringComparison.OrdinalIgnoreCase))
            {
                var signature = signatureWithPrefix.Substring(Sha1Prefix.Length);
                var secret = Encoding.ASCII.GetBytes(_gitConfig.WebhookSecret);
                var payloadBytes = Encoding.ASCII.GetBytes(payload);

                using (var hmSha1 = new HMACSHA1(secret))
                {
                    var hash = hmSha1.ComputeHash(payloadBytes);
                    var hashString = ToHexString(hash);

                    if (hashString.Equals(signature))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string ToHexString(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.AppendFormat("{0:x2}", b);
            }

            return builder.ToString();
        }

        public IList<string> GetBranches()
        {
            IList<string> branches;
            using (var repo = LocalRepo)
            {
                branches = repo.Branches.Where(x => !x.IsRemote).Select(x => x.FriendlyName).ToList();
            }

            return branches;
        }

        public string GetCurrentBranch()
        {
            string branch;
            using (var repo = LocalRepo)
            {
                var currentBranch = repo.Branches.FirstOrDefault(x => x.IsCurrentRepositoryHead);
                branch = currentBranch?.FriendlyName;
            }

            return branch;
        }

        public string GetCurrentCommitId()
        {
            string commitId;
            using (var repo = LocalRepo)
            {
                var currentBranch = repo.Branches.First(x => x.IsCurrentRepositoryHead);
                commitId = currentBranch.Tip.Id.ToString();
            }

            return commitId;
        }

        public Task ChangeBranch(string branch)
        {
            return Task.Run(async () =>
            {
                using (var repo = LocalRepo)
                {
                    var currentBranch = repo.Branches.First(x => x.IsCurrentRepositoryHead);
                    if (currentBranch.FriendlyName.Equals(branch)) return;

                    var branchToChangeTo = repo.Branches.First(x => x.FriendlyName.Equals(branch));
                    Commands.Checkout(repo, branchToChangeTo);
                }
            });
        }

        public void CreateBranch(string branch)
        {
            using (var repo = LocalRepo)
            {
                var newBranch = repo.CreateBranch(branch);

                // set the remote tracking branch
                repo.Branches.Update(newBranch,
                    b => b.Remote = "origin",
                    b => b.UpstreamBranch = newBranch.CanonicalName);

                Commands.Checkout(repo, newBranch);
            }
        }

        //public string Stash(string message, string email, string userName = null)
        //{
        //    string commitId;
        //    using (var repo = LocalRepo)
        //    {
        //        var signature = new Signature(
        //            new Identity(userName ?? email, email), DateTimeOffset.Now);

        //        try
        //        {
        //          var stash = repo.Stashes.Add(signature, message, StashModifiers.IncludeUntracked);
        //          commitId = stash.Index.Id.ToString();
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, $"Could not stash changes for message {message}");
        //            return null;
        //        }
        //    }

        //    return commitId;
        //}        

        //public StashApplyStatus ApplyStash(int index)
        //{
        //    using (var repo = LocalRepo)
        //    {
        //        try
        //        {
        //          var status = repo.Stashes.Apply(index);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, $"Could not apply stash for index {index}");
        //            return StashApplyStatus.NotFound;
        //        }
        //    }

        //    return status;
        //}

        public IList<string> GetUnstagedItems()
        {
            IList<string> unstagedFiles = new List<string>();
            using (var repo = LocalRepo)
            {
                var statusOptions = new StatusOptions
                {
                    Show = StatusShowOption.WorkDirOnly,
                    IncludeIgnored = false
                };

                foreach (var item in repo.RetrieveStatus(statusOptions))
                {
                    _logger.LogDebug($"Getting unstaged item {item.FilePath}");
                    unstagedFiles.Add(item.FilePath);
                }
            }

            return unstagedFiles;
        }

        public IList<string> GetStagedItems()
        {
            IList<string> stagedFiles = new List<string>();

            try
            {
                using (var repo = LocalRepo)
                {
                    var statusOptions = new StatusOptions
                    {
                        Show = StatusShowOption.IndexOnly,
                        IncludeIgnored = false
                    };

                    foreach (var item in repo.RetrieveStatus(statusOptions))
                    {
                        _logger.LogDebug($"Getting staged item {item.FilePath}");
                        stagedFiles.Add(item.FilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Could not get staged content items {0}", ex);
                return new List<string>();
            }

            return stagedFiles;
        }

        public string Commit(string message, string email, string userName = null)
        {
            Commit commit = null;
            using (var repo = LocalRepo)
            {
                try
                {
                    // Create the committer's signature and commit
                    Signature author = new Signature(userName ?? email, email, DateTimeOffset.Now);
                    Signature committer = author;

                    // Commit to the repository
                    commit = repo.Commit(message, author, committer);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not commit files for message {0}, error: {1}", message, ex);
                    return null;
                }
            }

            return commit.Id.ToString();
        }

        public bool Stage(params string[] filePaths)
        {
            using (var repo = LocalRepo)
            {
                try
                {
                    foreach (var filePath in filePaths)
                    {
                        Commands.Stage(repo, filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not stage files {0}, error: {1}", string.Join(",", filePaths), ex);
                    return false;
                }
            }

            return true;
        }

        public int Count(IList<string> paths = null)
        {
            int count;
            using (var repo = LocalRepo)
            {
                if (paths != null && paths.Any())
                {
                    count = paths.Sum(path => repo.Commits.QueryBy(path).Count());
                }
                else
                {
                    count = repo.Commits.Count();
                }
            }

            return count;
        }

        public void CheckoutPaths(string commitId, params string[] filePaths)
        {
            using (var repo = LocalRepo)
            {
                try
                {
                    repo.CheckoutPaths(commitId, filePaths);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Could not checkout paths ${string.Join(",", filePaths)} for commit {commitId}");
                }
            }
        }

        public IList<GitDiff> GetDiff(IList<string> paths, string id, string endId = null)
        {
            IList<PatchEntryChanges> patchEntryChanges = new List<PatchEntryChanges>();
            using (var repo = LocalRepo)
            {
                List<Commit> commitList = new List<Commit>();

                if (paths == null || !paths.Any())
                {
                    commitList = repo.Commits.ToList();
                }
                else
                {
                    foreach (var path in paths)
                    {
                        commitList.AddRange(repo.Commits.QueryBy(path)
                            .ToList()
                            .Select(entry => entry.Commit)
                            .ToList());
                    }
                }

                commitList.Add(null); // Added to show correct initial add

                var commitToView = commitList.First(x => x.Id.ToString().Equals(id));
                int indexOfCommit = commitList.IndexOf(commitToView);

                Patch repoDifferences;
                if (!string.IsNullOrEmpty(endId))
                {
                    var endCommit = commitList.First(x => x.Id.ToString().Equals(endId));
                    var indexOfEndCommit = commitList.IndexOf(endCommit);
                    repoDifferences =
                        repo.Diff.Compare<Patch>((Equals(commitList[indexOfCommit], null))
                            ? null
                            : commitList[indexOfCommit].Tree, (Equals(commitList[indexOfEndCommit], null)) ? null : commitList[indexOfEndCommit].Tree);
                }
                else
                {
                    endId = commitList[indexOfCommit + 1] != null ? commitList[indexOfCommit + 1].Id.ToString() : null;
                    repoDifferences =
                        repo.Diff.Compare<Patch>((Equals(commitList[indexOfCommit + 1], null))
                            ? null
                            : commitList[indexOfCommit + 1].Tree, (Equals(commitList[indexOfCommit], null)) ? null : commitList[indexOfCommit].Tree);
                }

                try
                {
                    if (paths == null || !paths.Any())
                    {
                        patchEntryChanges = repoDifferences.ToList();
                    }
                    else
                    {
                        foreach (var path in paths)
                        {
                            var changes = repoDifferences.Where(e => e.Path.Contains(path)).ToList();
                            foreach (var change in changes)
                            {
                                patchEntryChanges.Add(change);
                            }
                        }
                    }
                }
                catch { } // If the file has been renamed in the past- this search will fail
            }

            return GetGitDiffs(patchEntryChanges, endId, id);
        }

        public IList<GitDiff> GetDiffBetweenBranches(string id, string endId)
        {
            IList<PatchEntryChanges> patchEntryChanges = new List<PatchEntryChanges>();
            using (var repo = LocalRepo)
            {
                List<Commit> commitList = repo.Branches.SelectMany(x => x.Commits).ToList();
                commitList.Add(null); // Added to show correct initial add

                var commitToView = commitList.First(x => x.Id.ToString().Equals(id));
                int indexOfCommit = commitList.IndexOf(commitToView);

                var endCommit = commitList.First(x => x.Id.ToString().Equals(endId));
                var indexOfEndCommit = commitList.IndexOf(endCommit);
                var repoDifferences = repo.Diff.Compare<Patch>((Equals(commitList[indexOfCommit], null))
                    ? null
                    : commitList[indexOfCommit].Tree, (Equals(commitList[indexOfEndCommit], null)) ? null : commitList[indexOfEndCommit].Tree);

                try
                {
                    patchEntryChanges = repoDifferences.ToList();
                }
                catch { } // If the file has been renamed in the past- this search will fail
            }

            return GetGitDiffs(patchEntryChanges, endId, id);
        }

        public IList<GitDiff> GetDiffForStash(string id)
        {
            IList<PatchEntryChanges> patchEntryChanges = new List<PatchEntryChanges>();
            using (var repo = LocalRepo)
            {
                List<Commit> commitList = repo.Commits.ToList();
                Stash stash = repo.Stashes.First(x => x.Index.Id.ToString().Equals(id));
                commitList.Add(stash.Index);
                commitList.Add(null); // Added to show correct initial add

                var commitToView = commitList.First(x => x.Id.ToString().Equals(id));
                int indexOfCommit = commitList.IndexOf(commitToView);

                var endCommit = commitList.First(x => x.Id.ToString().Equals(GetCurrentCommitId()));
                var indexOfEndCommit = commitList.IndexOf(endCommit);
                var repoDifferences = repo.Diff.Compare<Patch>((Equals(commitList[indexOfCommit], null))
                    ? null
                    : commitList[indexOfCommit].Tree, (Equals(commitList[indexOfEndCommit], null)) ? null : commitList[indexOfEndCommit].Tree);

                try
                {
                    patchEntryChanges = repoDifferences.ToList();
                }
                catch { } // If the file has been renamed in the past- this search will fail
            }

            return GetGitDiffs(patchEntryChanges, id, GetCurrentCommitId());
        }

        public IList<GitDiff> GetDiffFromHead(IList<string> paths = null)
        {
            IList<PatchEntryChanges> patchEntryChanges = new List<PatchEntryChanges>();
            using (var repo = LocalRepo)
            {
                if (repo.Head.Tip == null) return new List<GitDiff>();

                var repoDifferences = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);

                try
                {
                    if (paths != null && paths.Any())
                    {
                        foreach (var path in paths)
                        {
                            var changes = repoDifferences.Where(e => e.Path.Contains(path)).ToList();
                            foreach (var change in changes)
                            {
                                patchEntryChanges.Add(change);
                            }
                        }
                    }
                    else
                    {
                        patchEntryChanges = repoDifferences.ToList();
                    }
                }
                catch { } // If the file has been renamed in the past- this search will fail
            }

            return GetGitDiffs(patchEntryChanges, GetCurrentCommitId());
        }

        private IList<GitDiff> GetGitDiffs(IList<PatchEntryChanges> patchEntryChanges, string commitId, string endCommitId = null)
        {
            IList<GitDiff> diffs = new List<GitDiff>();

            foreach (var patchEntryChange in patchEntryChanges)
            {
                string patch = patchEntryChange.Patch;
                var fileDiffMatch = Regex.Match(patch, "^diff(.*)", RegexOptions.Multiline);
                var indexMatch = Regex.Match(patch, "^index(.*)", RegexOptions.Multiline);
                var startFileMatch = Regex.Match(patch, "^---(.*)", RegexOptions.Multiline);
                var endFileMatch = Regex.Match(patch, "^\\+\\+\\+(.*)", RegexOptions.Multiline);

                string finalFileContent;
                if (patchEntryChange.Status == ChangeKind.Deleted)
                {
                    finalFileContent = null;
                }
                else if (!string.IsNullOrEmpty(endCommitId))
                {
                    finalFileContent = GetFileFromCommit(endCommitId, patchEntryChange.Path);
                }
                else
                {
                    finalFileContent = File.ReadAllText(Path.Combine(_localPathFactory.GetLocalPath(), patchEntryChange.Path));
                }

                var diff = new GitDiff
                {
                    FinalFileContent = finalFileContent,
                    FinalFile = endFileMatch.Value,
                    Index = indexMatch.Value,
                    FileDiff = fileDiffMatch.Value,
                    InitialFileContent = patchEntryChange.Status == ChangeKind.Added
                        ? null
                        : GetFileFromCommit(commitId, patchEntryChange.OldPath),
                    InitialFile = startFileMatch.Value,
                    Path = patchEntryChange.Path,
                    ChangeKind = patchEntryChange.Status
                };

                var gitDiffCalculationMatches = Regex.Matches(patch, "^@@(.*)@@", RegexOptions.Multiline);
                for (int i = 0; i < gitDiffCalculationMatches.Count; i++)
                {
                    var gitDiffChangeCalculations = new GitDiffChangeCalculations
                    {
                        ChangeCalculations = gitDiffCalculationMatches[i].Value
                    };

                    int startCharacter = patch.IndexOf(gitDiffCalculationMatches[i].Value, StringComparison.Ordinal);
                    startCharacter = startCharacter + gitDiffCalculationMatches[i].Value.Length;

                    int endCharacter = patchEntryChange.Patch.Length - 1;

                    if ((i + 1) < gitDiffCalculationMatches.Count)
                    {
                        endCharacter = patch.IndexOf(gitDiffCalculationMatches[i + 1].Value, StringComparison.Ordinal);
                    }

                    int length = endCharacter - startCharacter;
                    string changes = patch.Substring(startCharacter, length);
                    string[] lines = changes.Split(
                        new[] { '\n' },
                        StringSplitOptions.None
                    );

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;

                        var minusMatch = Regex.Match(line, "^(-[^-].*)");
                        var plusMatch = Regex.Match(line, "^([+][^+].*)");

                        GitDiffLineType lineType = GitDiffLineType.None;
                        if (minusMatch.Success)
                        {
                            lineType = GitDiffLineType.Subtraction;
                        }
                        else if (plusMatch.Success)
                        {
                            lineType = GitDiffLineType.Addition;
                        }

                        gitDiffChangeCalculations.GitDiffLines.Add(new GitDiffLine
                        {
                            Text = line,
                            LineType = lineType
                        });
                    }

                    diff.GitDiffChangeCalculations.Add(gitDiffChangeCalculations);
                }

                diffs.Add(diff);
            }

            return diffs;
        }



        public List<GitCommit> GetCommits(int page = 1, int take = 10, IList<string> paths = null)
        {
            List<GitCommit> commits = new List<GitCommit>();
            int skip = (page - 1) * take;
            using (var repo = LocalRepo)
            {
                Branch? remoteTrackingBranch = GetRemoteTrackingBranch(repo);
                if (paths != null && paths.Any())
                {
                    foreach (var path in paths)
                    {
                        foreach (var logEntry in repo.Commits.QueryBy(path))
                        {
                            bool isPushed = remoteTrackingBranch != null &&
                                repo.ObjectDatabase.FindMergeBase(logEntry.Commit, remoteTrackingBranch.Tip) == logEntry.Commit;

                            commits.Add(new GitCommit
                            {
                                Author = logEntry.Commit.Author.Name,
                                Date = logEntry.Commit.Author.When,
                                Message = logEntry.Commit.Message,
                                Id = logEntry.Commit.Id.ToString(),
                                Published = isPushed
                            });
                        }
                    }

                    commits =
                        commits.OrderByDescending(x => x.Date)
                            .Skip(skip)
                            .Take(take)
                            .ToList();
                }
                else
                {
                    foreach (var commit in repo.Commits.Skip(skip).Take(take))
                    {
                        bool isPushed = remoteTrackingBranch != null &&
                                        repo.ObjectDatabase.FindMergeBase(commit, remoteTrackingBranch.Tip) == commit;

                        commits.Add(new GitCommit
                        {
                            Author = commit.Author.Name,
                            Date = commit.Author.When,
                            Message = commit.Message,
                            Id = commit.Id.ToString(),
                            Published = isPushed
                        });
                    }
                }
            }

            return commits;
        }

        public IList<GitCommit> GetAllCommitsForPath(string path)
        {
            List<GitCommit> commits = new List<GitCommit>();
            using (var repo = LocalRepo)
            {
                foreach (var logEntry in repo.Commits.QueryBy(path))
                {
                    commits.Add(new GitCommit
                    {
                        Author = logEntry.Commit.Author.Name,
                        Date = logEntry.Commit.Author.When,
                        Message = logEntry.Commit.Message,
                        Id = logEntry.Commit.Id.ToString()
                    });
                }
            }

            return commits.OrderByDescending(x => x.Date).ToList();
        }

        public bool Unstage(params string[] filePaths)
        {
            using (var repo = LocalRepo)
            {
                try
                {
                    foreach (var filePath in filePaths)
                    {
                        Commands.Unstage(repo, filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Could not unstage files {0}, error {1}", string.Join(",", filePaths), ex);
                    return false;
                }
            }

            return true;
        }

        public bool Reset(ResetMode resetMode, string commitId = null)
        {
            using (var repo = LocalRepo)
            {
                try
                {
                    if (!string.IsNullOrEmpty(commitId))
                    {
                        repo.Reset(resetMode, commitId);
                    }
                    else
                    {
                        repo.Reset(resetMode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not reset repository");
                    return false;
                }
            }

            return true;
        }

        public bool ResetFileChanges(string path, string committishOrBranchSpec = null)
        {
            using (var repo = LocalRepo)
            {
                try
                {
                    // Reset file to HEAD state (discards working directory changes)
                    repo.CheckoutPaths(committishOrBranchSpec ?? "HEAD", new[] { path },
                        new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not reset file {0}", path);
                    return false;
                }
            }

            return true;
        }

        public bool RepositoryExists()
        {
            string localPath = _localPathFactory.GetLocalPath();
            return Directory.Exists(localPath) && Repository.IsValid(localPath);
        }

        private Branch? GetRemoteTrackingBranch(Repository repo)
        {
            var currentBranch = GetCurrentBranch();
            if (string.IsNullOrEmpty(currentBranch))
                return null;

            Branch? remoteTrackingBranch = repo.Branches[$"origin/{currentBranch}"];
            return remoteTrackingBranch;
        }

        public bool InitializeRepository(string folderPath, bool bare = false)
        {
            try
            {
                // Check if directory exists, create if it doesn't
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Check if a valid repository already exists
                if (Repository.IsValid(folderPath))
                {
                    _logger.LogInformation($"Repository already exists at {folderPath}");
                    return true;
                }

                // Initialize new repository
                Repository.Init(folderPath, bare);
                _logger.LogInformation($"Successfully initialized {(bare ? "bare " : "")}repository at {folderPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Could not initialize repository at {folderPath}");
                return false;
            }
        }
    }
}