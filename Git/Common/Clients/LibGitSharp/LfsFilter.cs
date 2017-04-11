using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Inedo.Extensions.Clients.LibGitSharp
{
    internal sealed class LfsFilter : Filter
    {
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
            using (var process = Process.Start(new ProcessStartInfo("git-lfs", $"{mode} -- {path}")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = root
            }))
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
                    throw new InvalidOperationException("git-lfs is not installed on the current server, but it is required by this repository. See https://git-lfs.github.com/ for installation instructions.");
                }
                else if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($"git-lfs exited with code {process.ExitCode}\nMessages: {errorText}");
                }
            }
        }
    }
}
