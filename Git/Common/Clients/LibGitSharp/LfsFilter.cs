using Inedo.ExecutionEngine.Executer;
using LibGit2Sharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using Inedo.IO;

namespace Inedo.Extensions.Clients.LibGitSharp
{
    internal sealed class LfsFilter : Filter
    {
        internal readonly List<GitRepositoryInfo> Repositories = new List<GitRepositoryInfo>();

        public LfsFilter(string name, IEnumerable<FilterAttributeEntry> attributes) : base(name, attributes)
        {
        }

        protected override void Clean(string path, string root, Stream input, Stream output)
        {
            this.CleanOrSmudgeAsync(path, root, input, output, "clean").WaitAndUnwrapExceptions();
        }

        protected override void Smudge(string path, string root, Stream input, Stream output)
        {
            this.CleanOrSmudgeAsync(path, root, input, output, "smudge").WaitAndUnwrapExceptions();
        }

        private async Task CleanOrSmudgeAsync(string path, string root, Stream input, Stream output, string mode)
        {
            using (var repo = new Repository(root))
            {
                var originUrl = repo.Network.Remotes["origin"].Url;

                try
                {
                    var info = this.GetInfo(root);
                    repo.Network.Remotes.Update("origin", r => r.Url = info.GetRemoteUrlWithCredentials());

                    using (var process = CreateProcess("git-lfs", $"{mode} -- {path}", root))
                    {
                        var inputCopy = input.CopyToAsync(process.StandardInput.BaseStream);
                        var outputCopy = process.StandardOutput.BaseStream.CopyToAsync(output);
                        var errorRead = process.StandardError.ReadToEndAsync();

                        await inputCopy.ConfigureAwait(false);
                        process.StandardInput.Close();
                        await outputCopy.ConfigureAwait(false);
                        process.StandardOutput.Close();

                        await process.DelayUntilExitAsync().ConfigureAwait(false);

                        var errorText = await errorRead.ConfigureAwait(false);

                        if (process.ExitCode == 9009)
                        {
                            throw new ExecutionFailureException("git-lfs is not installed on the current server, but it is required by this repository. See https://git-lfs.github.com/ for installation instructions.");
                        }
                        else if (process.ExitCode != 0)
                        {
                            throw new ExecutionFailureException($"git-lfs exited with code {process.ExitCode}\nMessages: {errorText}");
                        }
                    }
                }
                finally
                {
                    repo.Network.Remotes.Update("origin", r => r.Url = originUrl);
                }
            }
        }

        private GitRepositoryInfo GetInfo(string root)
        {
            var canonicalRoot = PathEx.MakeCanonical(root, '/');
            lock (this.Repositories)
            {
                foreach (var info in this.Repositories)
                {
                    var canonicalPath = PathEx.MakeCanonical(info.LocalRepositoryPath, '/');
                    if (string.Equals(canonicalRoot, canonicalPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return info;
                    }

                    if (canonicalPath.StartsWith(PathEx.EnsureTrailingDirectorySeparator(canonicalRoot), StringComparison.OrdinalIgnoreCase))
                    {
                        using (var repo = new Repository(canonicalPath))
                        {
                            return new GitRepositoryInfo(new WorkspacePath(root), repo.Network.Remotes["origin"].Url, info.UserName, info.Password);
                        }
                    }
                }
                throw new InvalidOperationException($"No known repository for path {root}\nKnown paths: {string.Join(", ", this.Repositories.Select(r => r.LocalRepositoryPath))}");
            }
        }

        private static Process CreateProcess(string fileName, string args, string workingDirectory)
        {
            try
            {
                return Process.Start(
                    new ProcessStartInfo(fileName, args)
                    {
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = workingDirectory
                    }
                );
            }
            catch
            {
                throw new ExecutionFailureException("git-lfs is not installed on the current server, but it is required by this repository. See https://git-lfs.github.com/ for installation instructions.");
            }
        }
    }
}
