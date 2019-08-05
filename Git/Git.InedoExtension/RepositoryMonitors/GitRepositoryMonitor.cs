using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.RepositoryMonitors;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Clients.CommandLine;
using Inedo.Extensions.Clients.LibGitSharp.Remote;
using Inedo.Extensions.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.Git.RepositoryMonitors
{
    [DisplayName("Git")]
    [Description("Monitors a Git repository for new commits.")]
    public sealed class GitRepositoryMonitor : RepositoryMonitor, IHasCredentials<GitCredentials>
    {
        [Persistent]
        [DisplayName("Repository URL")]
        [PlaceholderText("Use repository from credentials")]
        [MappedCredential(nameof(GitCredentials.RepositoryUrl))]
        public string RepositoryUrl { get; set; }

        [Persistent]
        [DisplayName("User name")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(GitCredentials.UserName))]
        public string UserName { get; set; }

        [Persistent]
        [DisplayName("Password")]
        [Category("Connection/Identity")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(GitCredentials.Password))]
        public SecureString Password { get; set; }

        [Persistent]
        [DisplayName("Credentials")]
        [Category("Connection/Identity")]
        public string CredentialName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [DisplayName("Git executable path")]
        [DefaultValue("$DefaultGitExePath")]
        public string GitExePath { get; set; }

        public override async Task<IReadOnlyDictionary<string, RepositoryCommit>> GetCurrentCommitsAsync(IRepositoryMonitorContext context)
        {
            var client = await this.CreateClientAsync(context).ConfigureAwait(false);
            var branches = await client.EnumerateRemoteBranchesAsync().ConfigureAwait(false);

            var results = new Dictionary<string, RepositoryCommit>();

            foreach (var b in branches)
                results[b.Name] = new GitRepositoryCommit { Hash = b.CommitHash };

            return results;
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "Git repository at ",
                new Hilite(AH.CoalesceString(this.RepositoryUrl, this.CredentialName))
            );
        }

        private async Task<GitClient> CreateClientAsync(IRepositoryMonitorContext context)
        {
            if (!string.IsNullOrEmpty(this.GitExePath))
            {
                this.LogDebug($"Executable path specified, using Git command line client at \"{this.GitExePath}\"...");
                return new GitCommandLineClient(
                    this.GitExePath,
                    await context.Agent.GetServiceAsync<IRemoteProcessExecuter>().ConfigureAwait(false),
                    await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false),
                    new GitRepositoryInfo(this.RepositoryUrl, this.UserName, this.Password),
                    this,
                    context.CancellationToken
                );
            }
            else
            {
                this.LogDebug("No executable path specified, using built-in Git library...");
                return new RemoteLibGitSharpClient(
                    await context.Agent.TryGetServiceAsync<IRemoteJobExecuter>().ConfigureAwait(false),
                    null,
                    false,
                    context.CancellationToken,
                    new GitRepositoryInfo(this.RepositoryUrl, this.UserName, this.Password),
                    this
                );
            }
        }
    }
}
