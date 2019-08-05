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
using Inedo.Extensions.Git.RepositoryMonitors;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.GitHub.RepositoryMonitors
{
    [DisplayName("GitHub")]
    [Description("Monitors a GitHub repository for new commits.")]
    public sealed class GitHubRepositoryMonitor : RepositoryMonitor, IHasCredentials<GitHubCredentials>
    {
        [Persistent]
        [Required]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
        public string ApiUrl { get; set; }
        [MappedCredential(nameof(GitHubCredentials.UserName))]
        public string UserName { get; set; }
        [MappedCredential(nameof(GitHubCredentials.Password))]
        public SecureString Password { get; set; }

        [Persistent]
        [DisplayName("Organization name")]
        [PlaceholderText("e.g. apache")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        public string OrganizationName { get; set; }

        [Persistent]
        [DisplayName("Repository")]
        [PlaceholderText("e.g. log4net")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        public string RepositoryName { get; set; }

        public override async Task<IReadOnlyDictionary<string, RepositoryCommit>> GetCurrentCommitsAsync(IRepositoryMonitorContext context)
        {
            var client = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);
            var branches = await client.ListRefsAsync(AH.CoalesceString(this.OrganizationName, this.UserName), this.RepositoryName, RefType.Branch, context.CancellationToken).ConfigureAwait(false);

            var results = new Dictionary<string, RepositoryCommit>();

            foreach (var b in branches)
                results[b.Name] = new GitRepositoryCommit { Hash = b.Hash };

            return results;
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription(
                "GitHub: ",
                new Hilite(AH.CoalesceString(this.RepositoryUrl, this.CredentialName))
            );
        }
    }
}
