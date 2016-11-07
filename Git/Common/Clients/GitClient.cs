using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Inedo.Diagnostics;

namespace Inedo.Extensions.Clients
{
    public abstract class GitClient
    {
        protected ILogger log;
        protected GitRepositoryInfo repository;

        protected GitClient(GitRepositoryInfo repository, ILogger log)
        {
            if (repository == null)
                throw new ArgumentNullException(nameof(repository));
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            this.repository = repository;
            this.log = log;
        }

        public abstract Task<bool> IsRepositoryValidAsync();
        public abstract Task CloneAsync(GitCloneOptions options);
        public abstract Task UpdateAsync(GitUpdateOptions options);
        public abstract Task ArchiveAsync(string targetDirectory);
        public abstract Task<IEnumerable<string>> EnumerateRemoteBranchesAsync();
        public abstract Task TagAsync(string tag);
    }
}
