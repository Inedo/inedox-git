using System;
using System.ComponentModel;
using System.Linq;
using System.Security;
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
    [DisplayName("Tag GitHub Source")]
    [Description("Tags the source code in a GitHub repository.")]
    [Tag("source-control")]
    [ScriptAlias("Tag")]
    [ScriptAlias("GitHub-Tag", Obsolete = true)]
    [ScriptNamespace("GitHub", PreferUnqualified = false)]
    [Example(@"
# tags the current source tree with the current release name and package number
GitHub::Tag(
    Credentials: Hdars-GitHub,
    Organization: Hdars,
    Tag: $ReleaseName.$PackageNumber
);
")]
    public sealed class GitHubTagOperation : TagOperation, IGitHubConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitHubSecureResource>))]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitHub resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitHub resource's credentials")]
        public SecureString Password { get; set; }

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
        
        private GitHubSecureCredentials credential;
        private GitHubSecureResource resource;

        public override Task ExecuteAsync(IOperationExecutionContext context)
        {
            (this.credential, this.resource) = this.GetCredentialsAndResource((ICredentialResolutionContext)context);
            return base.ExecuteAsync(context);
        }
        protected override Extensions.Credentials.UsernamePasswordCredentials GetCredentials() => this.credential?.ToUsernamePassword();

        protected override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) 
            => this.resource.GetRepositoryUrlAsync(context, cancellationToken);
        protected override ExtendedRichDescription GetDescription(IOperationConfiguration config)
        {
            return new ExtendedRichDescription(
               new RichDescription("Tag GitHub Source"),
               new RichDescription("in ", new Hilite(config.DescribeSource()), " with ", new Hilite(config[nameof(this.Tag)]))
            );
        }
    }
}
