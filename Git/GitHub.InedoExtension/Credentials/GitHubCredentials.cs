using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Credentials
{
    [ScriptAlias(GitHubCredentials.TypeName)]
    [DisplayName("GitHub")]
    [Description("Credentials for GitHub.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitHubCredentials,GitHub")]
    public sealed class GitHubCredentials : GitCredentialsBase
    {
        public const string TypeName = "GitHub";

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

        internal static GitHubCredentials TryCreate(string name, ValueEnumerationContext context)
        {
            return (GitHubCredentials)ResourceCredentials.TryCreate(GitHubCredentials.TypeName, name, environmentId: null, applicationId: context.ProjectId, inheritFromParent: false);
        }

        internal static GitHubCredentials TryCreate(string name, IComponentConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitHubCredentials)ResourceCredentials.TryCreate(GitHubCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }
        internal static GitHubCredentials TryCreate(string name, IOperationConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitHubCredentials)ResourceCredentials.TryCreate(GitHubCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }
    }
}
