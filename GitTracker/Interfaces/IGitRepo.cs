using System.Collections.Generic;
using System.Threading.Tasks;
using GitTracker.Models;
using LibGit2Sharp;

namespace GitTracker.Interfaces
{
    public interface IGitRepo
    {
        IList<string> GetUnstagedItems();
        IList<string> GetStagedItems();
        string Commit(string message, string email, string userName = null);
        bool Stage(params string[] filePaths);
        bool Unstage(params string[] filePaths);
        bool Pull(string email, string username = null);
        bool Push(string email, string username = null);
        bool Reset(ResetMode resetMode);
        List<GitCommit> GetCommits(int page = 1, int take = 10, IList<string> paths = null);
        int Count(string path);
        IList<GitDiff> GetDiff(IList<string> paths, string id, string endId = null);
        IList<GitDiff> GetDiffFromHead();
        IList<string> GetBranches();
        string GetCurrentBranch();
        Task ChangeBranch(string branch);
        void CreateBranch(string branch);
        bool IsGithubPushAllowed(string payload, string signatureWithPrefix);
    }
}