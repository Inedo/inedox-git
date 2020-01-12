using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Extensions.Operations;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Operations
{
    [DisplayName("Get Source from GitHub Repository")]
    [Description("Gets the source code from a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("Get-Source")]
    [ScriptAlias("GitHub-GetSource", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    [Example(@"
# pulls source from a remote repository and archives/exports the contents to a target directory
GitHub::Get-Source(
    Credentials: Hdars-GitHub,
    Organization: Hdars,
    DiskPath: ~\Sources
);
")]
    public sealed class GitHubGetSourceOperation : GetSourceOperation<GitHubCredentials>
    {
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public override string CredentialName { get; set; }

        [Category("GitHub")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        [PlaceholderText("Use organization from credentials")]
        [SuggestableValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Category("GitHub")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        [PlaceholderText("Use repository from credentials")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        protected override async Task<string> GetRepositoryUrlAsync(CancellationToken cancellationToken)
        {
            var github = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);

            var repo = (from r in await github.GetRepositoriesAsync(cancellationToken).ConfigureAwait(false)
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
               new RichDescription("from ", new Hilite(source), " to ", new Hilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
