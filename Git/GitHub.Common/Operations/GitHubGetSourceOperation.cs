using System;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Clients;
using System.Threading.Tasks;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Get Source from GitHub")]
    [Description("Gets the source code from a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("GitHub-GetSource")]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
GitHub-GetSource(
    Credentials: Hdars-GitHub,
    Organization: Hdars,
    DiskPath: ~\Sources
);
")]
    public sealed class GitHubGetSourceOperation : GetSourceOperation, IHasCredentials<GitHubCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Category("GitHub")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        [PlaceholderText("Use organization from credentials")]
        public string OrganizationName { get; set; }

        [Category("GitHub")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        [PlaceholderText("Use repository from credentials")]
        public string RepositoryName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        protected override async Task<string> GetRepositoryUrlAsync()
        {
            var github = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);

            var repo = (from r in await github.GetRepositoriesAsync().ConfigureAwait(false)
                       where string.Equals((string)r["name"], this.RepositoryName, StringComparison.OrdinalIgnoreCase)
                       select r).FirstOrDefault();

            if (repo == null)
                throw new InvalidOperationException($"Repository '{this.RepositoryName}' not found on GitHub.");

            return (string)repo["clone_url"];
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            string source = AH.CoalesceString(config[nameof(this.RepositoryName)], config[nameof(this.CredentialName)]);

            return new ExtendedRichDescription(
               new RichDescription("Get GitHub Source"),
               new RichDescription("from ", new Hilite(source), " to ", new Hilite(config[nameof(this.DiskPath)]))
            );
        }
    }
}
