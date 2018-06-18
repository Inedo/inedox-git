using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensions.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.Credentials
{
    [ScriptAlias("GitHub")]
    [DisplayName("GitHub")]
    [Description("Credentials for GitHub.")]
    public sealed class GitHubCredentials : GitCredentialsBase
    {
        [Persistent]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        public string ApiUrl { get; set; }

        [Persistent]
        [DisplayName("Organization name")]
        [PlaceholderText("e.g. apache")]
        [SuggestableValue(typeof(CredentialsOrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Persistent]
        [DisplayName("Repository")]
        [PlaceholderText("e.g. log4net")]
        [SuggestableValue(typeof(CredentialsRepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }
        
        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string RepositoryUrl { get; set; }

        public override RichDescription GetDescription()
        {
            var desc = new RichDescription(AH.CoalesceString(this.UserName, "Anonymous"), "@", "GitHub");
            if (!string.IsNullOrEmpty(this.OrganizationName))
                desc.AppendContent(",Organization=", this.OrganizationName);

            return desc;            
        }
    }
}
