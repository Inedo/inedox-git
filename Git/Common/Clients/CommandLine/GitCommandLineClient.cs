using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.IO;

namespace Inedo.Extensions.Clients.CommandLine
{
    public sealed class GitCommandLineClient : GitClient
    {
        private static readonly LazyRegex BranchParsingRegex = new LazyRegex(@"refs/heads/(?<branch>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline);

        private string gitExePath;
        private IRemoteProcessExecuter processExecuter;
        private IFileOperationsExecuter fileOps;
        private CancellationToken cancellationToken;

        public GitCommandLineClient(string gitExePath, IRemoteProcessExecuter processExecuter, IFileOperationsExecuter fileOps, GitRepositoryInfo repository, ILogger log, CancellationToken cancellationToken)
            : base(repository, log)
        {
            if (gitExePath == null)
                throw new ArgumentNullException(nameof(gitExePath));
            if (processExecuter == null)
                throw new ArgumentNullException(nameof(processExecuter));
            if (fileOps == null)
                throw new ArgumentNullException(nameof(fileOps));

            this.gitExePath = gitExePath;
            this.processExecuter = processExecuter;
            this.fileOps = fileOps;
            this.cancellationToken = cancellationToken;
        }

        public override async Task<bool> IsRepositoryValidAsync()
        {
            var result = await this.ExecuteCommandLineAsync(
                new GitArgumentsBuilder("log -n 1"),
                this.repository.LocalRepositoryPath
              ).ConfigureAwait(false);

            return result.ExitCode == 0 && result.Error.Count == 0;
        }

        public override async Task CloneAsync(GitCloneOptions options)
        {
            var args = new GitArgumentsBuilder();

            args.Append("clone");
            args.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            args.AppendQuoted(this.repository.LocalRepositoryPath);

            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override async Task UpdateAsync(GitUpdateOptions options)
        {
            /* 
             *  git fetch origin <branch> | Get all changesets for the specified branch but does not apply them
             *  git reset --hard <ref>    | Resets to the HEAD revision and removes commits that haven't been pushed
             *  git clean -dfq            | Remove all non-Git versioned files and directories from the repository directory
             */

            var args = new GitArgumentsBuilder("fetch");
            args.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            args.Append("--quiet");
            args.Append(options.Branch);

            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var resetArgs = new GitArgumentsBuilder("reset --hard");
            resetArgs.Append(AH.CoalesceString(options.Tag, "FETCH_HEAD"));

            await this.ExecuteCommandLineAsync(
                resetArgs,
                this.repository.LocalRepositoryPath
              ).ConfigureAwait(false);

            await this.ExecuteCommandLineAsync(
                new GitArgumentsBuilder("clean -dfq"),
                this.repository.LocalRepositoryPath
              ).ConfigureAwait(false);
        }

        public override async Task<IEnumerable<string>> EnumerateRemoteBranchesAsync()
        {
            var args = new GitArgumentsBuilder("ls-remote --refs --heads");
            args.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());

            var result = await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var branches = from o in result.Output
                           let value = BranchParsingRegex.Match(o).Groups["branch"].Value
                           where !string.IsNullOrEmpty(value)
                           select value;

            return branches;
        }

        public override async Task TagAsync(string tag)
        {
            var args = new GitArgumentsBuilder("tag -f");
            args.Append(tag);
            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var pushArgs = new GitArgumentsBuilder("push");
            pushArgs.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            pushArgs.Append("--tags --quiet");

            await this.ExecuteCommandLineAsync(pushArgs, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override Task ArchiveAsync(string targetDirectory)
        {
            return this.CopyNonGitFiles(this.repository.LocalRepositoryPath, targetDirectory);
        }

        private async Task<ProcessResults> ExecuteCommandLineAsync(GitArgumentsBuilder args, string workingDirectory)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.gitExePath,
                Arguments = args.ToString(),
                WorkingDirectory = workingDirectory
            };

            this.log.LogDebug("Ensuring local repository path exists...");
            await this.fileOps.CreateDirectoryAsync(this.repository.LocalRepositoryPath).ConfigureAwait(false);

            this.log.LogDebug("Working directory: " + startInfo.WorkingDirectory);
            this.log.LogDebug("Executing: " + startInfo.FileName + " " + args.ToSensitiveString());

            using (var process = this.processExecuter.CreateProcess(startInfo))
            {
                var outputLines = new List<string>();
                var errorLines = new List<string>();

                process.OutputDataReceived += (s, e) => { if (e?.Data != null) outputLines.Add(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e?.Data != null) errorLines.Add(e.Data); };

                process.Start();

                await process.WaitAsync(this.cancellationToken).ConfigureAwait(false);

                return new ProcessResults(process.ExitCode ?? -1, outputLines, errorLines);
            }
        }

        private async Task CopyNonGitFiles(string sourceDirectory, string targetDirectory)
        {
            if (!await this.fileOps.DirectoryExistsAsync(sourceDirectory).ConfigureAwait(false))
                return;
            
            char separator = this.fileOps.DirectorySeparator;

            var infos = await this.fileOps.GetFileSystemInfosAsync(sourceDirectory, new MaskingContext(new[] { "**" }, new[] { "**" + separator + ".git**" })).ConfigureAwait(false);

            var directoriesToCreate = infos.OfType<SlimDirectoryInfo>().Select(d => CombinePaths(targetDirectory, d.FullName.Substring(sourceDirectory.Length), separator)).ToArray();
            var relativeFileNames = infos.OfType<SlimFileInfo>().Select(f => f.FullName.Substring(sourceDirectory.Length).TrimStart(separator)).ToArray();

            await this.fileOps.CreateDirectoryAsync(targetDirectory).ConfigureAwait(false);

            foreach (string folder in directoriesToCreate)
                await this.fileOps.CreateDirectoryAsync(folder).ConfigureAwait(false);
            
            await this.fileOps.FileCopyBatchAsync(
                sourceDirectory,
                relativeFileNames,
                targetDirectory,
                relativeFileNames,
                true,
                true
            ).ConfigureAwait(false);
        }

        private static string CombinePaths(string p1, string p2, char separator)
        {
            return p1.TrimEnd(separator) + separator + p2.TrimStart(separator);
        }
    }
}
