using LibGit2Sharp;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GitTracker.Helpers;

public static class RepoHelpers
{
    internal static class NativeMethods
    {
        // From libgit2: int git_odb_refresh(git_odb *odb);
        [DllImport("git2-3f4182d", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int git_odb_refresh(IntPtr odb);
    }

    public static void ForceRefreshOdb(this Repository repo)
    {
        // 1. Access the internal ObjectDatabase
        var objectDatabase = repo.ObjectDatabase;

        // 2. Use reflection to get the internal 'odb' field (naming may vary)
        var odbField = typeof(ObjectDatabase).GetField("handle",
                           BindingFlags.NonPublic | BindingFlags.Instance);

        if (odbField == null)
        {
            throw new InvalidOperationException("Could not find internal ODB field.");
        }

        var odbValue = odbField.GetValue(objectDatabase);
        var handle = (SafeHandle)odbValue;

        // 4. Call the native function
        int result = NativeMethods.git_odb_refresh(handle.DangerousGetHandle());
        if (result != 0)
        {
            // Error handling for native libgit2 error
        }
    }
}