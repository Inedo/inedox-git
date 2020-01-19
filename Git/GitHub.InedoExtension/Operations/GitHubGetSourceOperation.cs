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
using Inedo.Serialization;
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
    public sealed class GitHubGetSourceOperation : GetSourceOperation, IGitHubConfiguration
    {
        [Persistent]
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitHubSecureResource>))]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [PlaceholderText("Use organization from Github resource")]
        [SuggestableValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Github resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Use URL from Github resource.")]
        public string ApiUrl { get; set; }

        protected override async Task<string> GetRepositoryUrlAsync(CancellationToken cancellationToken, ICredentialResolutionContext context)
        {
            var (credentials, resource) = this.GetCredentialsAndResource(context);
            var github = new GitHubClient(credentials, resource);

            var repo = (from r in await github.GetRepositoriesAsync(cancellationToken).ConfigureAwait(false)
                       where string.Equals((string)r["name"], resource.RepositoryName, StringComparison.OrdinalIgnoreCase)
                       select r).FirstOrDefault();

            if (repo == null)
                throw new InvalidOperationException($"Repository '{resource.RepositoryName}' not found on GitHub.");

            return (string)repo["clone_url"];
        }

        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Get GitHub Source"),
               new RichDescription("from ", new Hilite(config.DescribeSource()), " to ", new Hilite(AH.CoalesceString(config[nameof(this.DiskPath)], "$WorkingDirectory")))
            );
        }
    }
}
