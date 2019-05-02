using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.ListVariableSources
{
    [DisplayName("GitHub Branches")]
    [Description("Branches from a GitHub repository.")]
    public sealed class BranchListVariableSource : ListVariableSource, IHasCredentials<GitHubCredentials>
    {
        [Persistent]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        public string CredentialName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.UserName))]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.Password))]
        public SecureString Password { get; set; }

        [Persistent]
        [Category("GitHub")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        [PlaceholderText("Use organization from credentials")]
        [SuggestableValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Persistent]
        [Category("GitHub")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        [PlaceholderText("Use repository from credentials")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        public override Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            this.SetValues();

            var client = new GitHubClient(this.ApiUrl, this.UserName, this.Password, this.OrganizationName);

            return client.ListRefsAsync(this.OrganizationName, this.RepositoryName, RefType.Branch, CancellationToken.None);
        }

        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.OrganizationName, this.UserName) + "/" + this.RepositoryName;
            return new RichDescription("GitHub (", new Hilite(repoName), ") branches.");
        }
    }
}
