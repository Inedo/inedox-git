﻿using System.Text.RegularExpressions;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.ExecutionEngine.Executer;

namespace Inedo.Extensions.Clients.CommandLine
{
    internal sealed class GitCommandLineClient : GitClient
    {
        private static readonly LazyRegex BranchParsingRegex = new LazyRegex(@"^(?<1>[0-9a-fA-F]+)\s+refs/heads/(?<2>.+)$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private readonly string gitExePath;
        private readonly IRemoteProcessExecuter processExecuter;
        private readonly IFileOperationsExecuter fileOps;
        private readonly CancellationToken cancellationToken;

        public GitCommandLineClient(string gitExePath, IRemoteProcessExecuter processExecuter, IFileOperationsExecuter fileOps, GitRepositoryInfo repository, ILogSink log, CancellationToken cancellationToken)
            : base(repository, log)
        {
            this.gitExePath = gitExePath ?? throw new ArgumentNullException(nameof(gitExePath));
            this.processExecuter = processExecuter ?? throw new ArgumentNullException(nameof(processExecuter));
            this.fileOps = fileOps ?? throw new ArgumentNullException(nameof(fileOps));
            this.cancellationToken = cancellationToken;
        }

        public override async Task<bool> IsRepositoryValidAsync()
        {
            var result = await this.ExecuteCommandLineAsync(
                new GitArgumentsBuilder("log -n 1"),
                this.repository.LocalRepositoryPath,
                false
              ).ConfigureAwait(false);

            return result.ExitCode == 0 && result.Error.Count == 0;
        }

        public override async Task CloneAsync(GitCloneOptions options)
        {
            var args = new GitArgumentsBuilder("clone");

            if (options.Branch != null)
            {
                args.Append("-b");
                args.AppendQuoted(options.Branch);
            }
            if (options.RecurseSubmodules)
            {
                args.Append("--recursive");
            }

            args.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            args.AppendQuoted(this.repository.LocalRepositoryPath);

            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override async Task<string> UpdateAsync(GitUpdateOptions options)
        {
            /* 
             *  git remote set-url origin <url>         | Make sure we're talking to the correct remote repository
             *  git fetch origin                        | Update the local cache of the remote repository
             *  git reset --hard <ref>                  | Resets to the HEAD revision and removes commits that haven't been pushed
             *  git clean -dfq                          | Remove all non-Git versioned files and directories from the repository working directory
             *  git submodule update --init --recursive | Updates submodules to the version referenced by the HEAD revision
             */

            var remoteArgs = new GitArgumentsBuilder("remote set-url origin");
            remoteArgs.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());
            await this.ExecuteCommandLineAsync(remoteArgs, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            await this.ExecuteCommandLineAsync(new GitArgumentsBuilder("fetch origin"), this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var resetArgs = new GitArgumentsBuilder("reset --hard");
            if (options.Ref != null)
                resetArgs.AppendQuoted(options.Ref);
            else if (options.Branch != null)
                resetArgs.AppendQuoted("origin/" + options.Branch);
            else
                resetArgs.Append("FETCH_HEAD");

            await this.ExecuteCommandLineAsync(
                resetArgs,
                this.repository.LocalRepositoryPath
              ).ConfigureAwait(false);

            await this.ExecuteCommandLineAsync(
                new GitArgumentsBuilder("clean -dfq"),
                this.repository.LocalRepositoryPath
              ).ConfigureAwait(false);

            if (options.RecurseSubmodules)
            {
                await this.ExecuteCommandLineAsync(
                    new GitArgumentsBuilder("submodule update --init --recursive"),
                    this.repository.LocalRepositoryPath
                  ).ConfigureAwait(false);
            }

            var results = await this.ExecuteCommandLineAsync(
                new GitArgumentsBuilder("log -n 1 --format=%H"),
                this.repository.LocalRepositoryPath
            ).ConfigureAwait(false);

            return string.Join(" ", results.Output).Trim();
        }

        public override async Task<IEnumerable<RemoteBranchInfo>> EnumerateRemoteBranchesAsync()
        {
            var args = new GitArgumentsBuilder("ls-remote --refs --heads");
            args.AppendSensitive(this.repository.GetRemoteUrlWithCredentials());

            var result = await this.ExecuteCommandLineAsync(args, null, false).ConfigureAwait(false);

            var branches = new List<RemoteBranchInfo>();

            foreach (var o in result.Output)
            {
                var match = BranchParsingRegex.Match(o);
                if (match.Success)
                    branches.Add(new RemoteBranchInfo(match.Groups[2].Value, match.Groups[1].Value));
            }

            return branches;
        }

        public override async Task TagAsync(string tag, string commit, string message, bool force = false)
        {
            var args = new GitArgumentsBuilder("tag");
            if (force)
            {
                args.Append("-f");
            }
            if (!string.IsNullOrEmpty(message))
            {
                args.Append("-a");
                args.AppendQuoted(tag);
                args.Append("-m");
                args.AppendQuoted(message);
            }
            else
            {
                args.AppendQuoted(tag);
            }

            if (!string.IsNullOrEmpty(commit))
                args.AppendQuoted(commit);

            await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var pushArgs = new GitArgumentsBuilder("push origin");
            pushArgs.AppendQuoted(tag);
            pushArgs.Append("--quiet");
            if (force)
            {
                pushArgs.Append("--force");
            }

            await this.ExecuteCommandLineAsync(pushArgs, this.repository.LocalRepositoryPath).ConfigureAwait(false);
        }

        public override Task ArchiveAsync(string targetDirectory, bool keepInternals = false)
        {
            return CopyFilesAsync(this.fileOps, this.repository.LocalRepositoryPath, targetDirectory, keepInternals);
        }

        public override async Task<IReadOnlyList<string>> ListRepoFilesAsync()
        {
            var args = new GitArgumentsBuilder("ls-files --recurse-submodules");
            var results = await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);
            return results.Output.ToArray();
        }

        public override async Task<DateTimeOffset?> GetFileLastModifiedAsync(string fileName)
        {
            var args = new GitArgumentsBuilder("log --max-count=1 --pretty=format:%aI --");
            args.AppendQuoted(fileName);
            var results = await this.ExecuteCommandLineAsync(args, this.repository.LocalRepositoryPath).ConfigureAwait(false);

            var timestamp = results.Output.FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(timestamp))
                return null;

            return DateTimeOffset.Parse(timestamp);
        }

        private async Task<ProcessResults> ExecuteCommandLineAsync(GitArgumentsBuilder args, string workingDirectory, bool throwOnFailure = true)
        {
            var startInfo = new RemoteProcessStartInfo
            {
                FileName = this.gitExePath,
                Arguments = args.ToString(),
                WorkingDirectory = workingDirectory ?? await this.fileOps.GetBaseWorkingDirectoryAsync().ConfigureAwait(false)
            };

            if (this.repository.HasLocalRepository)
            {
                this.log.LogDebug("Ensuring local repository path exists...");
                await this.fileOps.CreateDirectoryAsync(startInfo.WorkingDirectory).ConfigureAwait(false);
            }

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

                if (throwOnFailure && process.ExitCode != 0)
                {
                    throw new ExecutionFailureException($"git returned error code {process.ExitCode}\n{string.Join("\n", errorLines)}");
                }
                return new ProcessResults(process.ExitCode ?? -1, outputLines, errorLines);
            }
        }
    }
}
