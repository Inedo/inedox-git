using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Inedo.Agents;
using Inedo.Diagnostics;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.RepositoryMonitors;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Clients.CommandLine;
using Inedo.Extensions.Clients.LibGitSharp.Remote;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Git.RepositoryMonitors
{
    [DisplayName("Git")]
    [Description("Monitors a Git repository for new commits.")]
    public sealed class GitRepositoryMonitor : RepositoryMonitor, IMissingPersistentPropertyHandler
    {
        [Required]
        [Persistent]
        [DisplayName("From Git resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitSecureResourceBase>))]
        public string ResourceName { get; set; }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (string.IsNullOrEmpty(this.ResourceName) && missingProperties.TryGetValue("CredentialName", out var value))
                this.ResourceName = value;
        }

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
                new Hilite(this.ResourceName)
            );
        }

        private async Task<GitClient> CreateClientAsync(IRepositoryMonitorContext context)
        {
            var credctx = (ICredentialResolutionContext)context;
            var resource = (GitSecureResourceBase)SecureResource.Create(this.ResourceName, credctx);
            var credential = resource?.GetCredentials(credctx);
            if (resource == null)
            {
                var rc = SecureCredentials.Create(this.ResourceName, credctx) as GitCredentialsBase;
                resource = (GitSecureResourceBase)rc?.ToSecureResource();
                credential = rc?.ToSecureCredentials();
            }

            var repositoryUrl = await resource.GetRepositoryUrlAsync(credctx, context.CancellationToken);
            var upcreds = credential as Extensions.Credentials.UsernamePasswordCredentials;
            if (credential is GitSecureCredentialsBase gitcreds)
                upcreds = gitcreds.ToUsernamePassword();
            else if (credential != null && upcreds == null)
                throw new InvalidOperationException("Invalid credential type for Git repository monitor.");

            if (!string.IsNullOrEmpty(this.GitExePath))
            {
                this.LogDebug($"Executable path specified, using Git command line client at \"{this.GitExePath}\"...");
                return new GitCommandLineClient(
                    this.GitExePath,
                    await context.Agent.GetServiceAsync<IRemoteProcessExecuter>().ConfigureAwait(false),
                    await context.Agent.GetServiceAsync<IFileOperationsExecuter>().ConfigureAwait(false),
                    new GitRepositoryInfo(repositoryUrl, upcreds?.UserName, upcreds?.Password),
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
                    new GitRepositoryInfo(repositoryUrl, upcreds?.UserName, upcreds?.Password),
                    this
                );
            }
        }
    }
}
