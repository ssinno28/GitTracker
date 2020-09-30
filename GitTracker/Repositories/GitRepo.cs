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

        public GitRepo(GitConfig gitConfig, ILoggerFactory loggerFactory)
        {
            _gitConfig = gitConfig;
            _logger = loggerFactory.CreateLogger<GitRepo>();
        }

        private Repository LocalRepo => new Repository(_gitConfig.LocalPath);
        private Repository RemoteRepo => new Repository(_gitConfig.RemotePath);

        public bool Pull(string email, string username = null)
        {
            if (string.IsNullOrEmpty(_gitConfig.Token))
            {
                _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because there is no token.");
                return false;
            }

            if (!Directory.Exists(_gitConfig.LocalPath) || !Repository.IsValid(_gitConfig.LocalPath))
            {
                Repository.Init(_gitConfig.LocalPath);
            }

            using (var repo = new Repository(_gitConfig.LocalPath))
            {
                if (repo.Network.Remotes["origin"] == null && string.IsNullOrEmpty(_gitConfig.RemotePath))
                {
                    _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because no origin is set.");
                    return false;
                }

                SetupAndTrack(repo, username ?? email);

                CredentialsHandler credentialsProvider = (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials
                    {
                        Username = username ?? email,
                        Password = _gitConfig.Token
                    };

                // Credential information to fetch
                PullOptions options = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = credentialsProvider
                    },
                    MergeOptions = new MergeOptions
                    {
                        FastForwardStrategy = FastForwardStrategy.FastForwardOnly
                    }
                };

                // User information to create a merge commit
                var signature = new Signature(
                    new Identity(username ?? email, email), DateTimeOffset.Now);

                // Pull
                Commands.Pull(repo, signature, options);
            }

            return true;
        }

        public bool Push(string email, string username = null)
        {
            if (string.IsNullOrEmpty(_gitConfig.Token))
            {
                _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because there is no token.");
                return false;
            }

            using (var repo = new Repository(_gitConfig.LocalPath))
            {
                if (repo.Network.Remotes["origin"] == null && string.IsNullOrEmpty(_gitConfig.RemotePath))
                {
                    _logger.LogWarning($"Could not pull from git repo ${_gitConfig.RemotePath} because no origin is set.");
                    return false;
                }

                SetupAndTrack(repo, username ?? email);

                CredentialsHandler credentialsProvider = (url, usernameFromUrl, types) =>
                    new UsernamePasswordCredentials
                    {
                        Username = username ?? email,
                        Password = _gitConfig.Token
                    };

                // Credential information to fetch
                PushOptions options = new PushOptions
                {
                    CredentialsProvider = credentialsProvider
                };

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

            CredentialsHandler credentialsProvider = (url, usernameFromUrl, types) =>
                new UsernamePasswordCredentials
                {
                    Username = userName,
                    Password = _gitConfig.Token
                };

            if (currentBranch == null)
            {
                FetchOptions fetchOptions =
                    new FetchOptions
                    {
                        CredentialsProvider = credentialsProvider
                    };

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
                var currentBranch = repo.Branches.First(x => x.IsCurrentRepositoryHead);
                branch = currentBranch.FriendlyName;
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

        public int Count(string path)
        {
            int count;
            using (var repo = LocalRepo)
            {
                count = !string.IsNullOrEmpty(path)
                    ? repo.Commits.QueryBy(path).Count()
                    : repo.Commits.Count();
            }

            return count;
        }

        public void CheckoutPaths(string commitId, params string[] filePaths)
        {
            using (var repo = LocalRepo)
            {
                repo.CheckoutPaths(commitId, filePaths);
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

            return GetGitDiffs(patchEntryChanges);
        }

        public IList<GitDiff> GetDiffFromHead()
        {
            IList<PatchEntryChanges> patchEntryChanges = new List<PatchEntryChanges>();
            using (var repo = LocalRepo)
            {
                var repoDifferences = repo.Diff.Compare<Patch>(repo.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory);

                try { patchEntryChanges = repoDifferences.ToList(); }
                catch { } // If the file has been renamed in the past- this search will fail
            }

            return GetGitDiffs(patchEntryChanges);
        }

        private IList<GitDiff> GetGitDiffs(IList<PatchEntryChanges> patchEntryChanges)
        {
            IList<GitDiff> diffs = new List<GitDiff>();

            foreach (var patchEntryChange in patchEntryChanges)
            {
                string patch = patchEntryChange.Patch;
                var fileDiffMatch = Regex.Match(patch, "^diff(.*)", RegexOptions.Multiline);
                var indexMatch = Regex.Match(patch, "^index(.*)", RegexOptions.Multiline);
                var startFileMatch = Regex.Match(patch, "^---(.*)", RegexOptions.Multiline);
                var endFileMatch = Regex.Match(patch, "^\\+\\+\\+(.*)", RegexOptions.Multiline);

                var diff = new GitDiff
                {
                    FinalFile = endFileMatch.Value,
                    Index = indexMatch.Value,
                    FileDiff = fileDiffMatch.Value,
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
                if (paths != null && paths.Any())
                {
                    foreach (var path in paths)
                    {
                        foreach (var logEntry in repo.Commits.QueryBy(path).Skip(skip).Take(take))
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
                }
                else
                {
                    foreach (var commit in repo.Commits.Skip(skip).Take(take))
                    {
                        commits.Add(new GitCommit
                        {
                            Author = commit.Author.Name,
                            Date = commit.Author.When,
                            Message = commit.Message,
                            Id = commit.Id.ToString()
                        });
                    }
                }
            }

            return commits;
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

        public bool Reset(ResetMode resetMode)
        {
            using (var repo = LocalRepo)
            {
                try
                {
                    repo.Reset(resetMode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not reset repository");
                    return false;
                }
            }

            return true;
        }
    }
}