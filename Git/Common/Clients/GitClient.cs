using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.Clients
{
    public abstract class GitClient
    {
        protected ILogSink log;
        protected GitRepositoryInfo repository;

        protected GitClient(GitRepositoryInfo repository, ILogSink log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public abstract Task<bool> IsRepositoryValidAsync();
        public abstract Task CloneAsync(GitCloneOptions options);
        public abstract Task<string> UpdateAsync(GitUpdateOptions options);
        public abstract Task PushAsync(GitPushOptions gitPushOptions);
        public abstract Task ArchiveAsync(string targetDirectory, bool keepInternals = false);
        public abstract Task<IEnumerable<string>> EnumerateRemoteBranchesAsync();
        public abstract Task TagAsync(string tag, string commit, string message, bool force = false);

        protected static async Task CopyFilesAsync(IFileOperationsExecuter fileOps, string sourceDirectory, string targetDirectory, bool keepInternals = false)
        {
            if (!await fileOps.DirectoryExistsAsync(sourceDirectory).ConfigureAwait(false))
                return;

            char separator = fileOps.DirectorySeparator;

            var infos = await fileOps.GetFileSystemInfosAsync(sourceDirectory, keepInternals ? MaskingContext.IncludeAll : new MaskingContext(new[] { "**" }, new[] { "**" + separator + ".git**", ".git**" })).ConfigureAwait(false);

            var directoriesToCreate = infos.OfType<SlimDirectoryInfo>().Select(d => CombinePaths(targetDirectory, d.FullName.Substring(sourceDirectory.Length), separator)).ToArray();
            var relativeFileNames = infos.OfType<SlimFileInfo>().Select(f => f.FullName.Substring(sourceDirectory.Length).TrimStart(separator)).ToArray();

            await fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);

            foreach (string folder in directoriesToCreate)
                await fileOps.CreateDirectoryAsync(folder).ConfigureAwait(false);

            await fileOps.FileCopyBatchAsync(
                sourceDirectory,
                relativeFileNames,
                targetDirectory,
                relativeFileNames,
                true,
                true
            ).ConfigureAwait(false);
        }

        private static string CombinePaths(string p1, string p2, char separator) => p1.TrimEnd(separator) + separator + p2.TrimStart(separator);
    }
}
