using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Extensibility.Operations;
using SuggestableValueAttribute = Inedo.BuildMaster.Web.Controls.SuggestableValueAttribute;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Extensibility.Operations;
using SuggestableValueAttribute = Inedo.Otter.Web.Controls.SuggestableValueAttribute;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Web;
#endif

namespace Inedo.Extensions.Operations
{
    [DisplayName("Tag GitHub Source")]
    [Description("Tags the source code in a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("GitHub-Tag")]
    [ScriptNamespace("GitHub", PreferUnqualified = true)]
    [Example(@"
# tags the current source tree with the current release name and package number
GitHub-Tag(
    Credentials: Hdars-GitHub,
    Organization: Hdars,
    Tag: $ReleaseName.$PackageNumber
);
")]
    public sealed class GitHubTagOperation : TagOperation<GitHubCredentials>
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
               new RichDescription("Tag GitHub Source"),
               new RichDescription("in ", new Hilite(source), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
