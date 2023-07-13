using System;
using System.Collections.Generic;
using System.Threading;

namespace Inedo.Extensions.Git.RaftRepositories
{
    internal static class GitRepoLock
    {
        private static readonly Dictionary<string, object> repoLocks = new Dictionary<string, object>();

        public static void EnterLock(string repoPath)
        {
            if (string.IsNullOrEmpty(repoPath))
                throw new ArgumentNullException(nameof(repoPath));

            object repoLock;

            lock (repoLocks)
            {
                if (!repoLocks.TryGetValue(repoPath, out repoLock))
                {
                    repoLock = new object();
                    repoLocks.Add(repoPath, repoLock);
                    Monitor.Enter(repoLock);
                    return;
                }
            }

            Monitor.Enter(repoLock);
        }

        public static void ReleaseLock(string repoPath)
        {
            if (string.IsNullOrEmpty(repoPath))
                return;

            lock (repoLocks)
            {
                if (repoLocks.TryGetValue(repoPath, out var repoLock))
                    Monitor.Exit(repoLock);
            }
        }
    }
}
