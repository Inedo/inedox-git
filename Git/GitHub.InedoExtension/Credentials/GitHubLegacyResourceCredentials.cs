using System.ComponentModel;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Serialization;

namespace Inedo.Extensions.GitHub.Credentials
{
    [ScriptAlias(GitHubLegacyResourceCredentials.TypeName)]
    [DisplayName("GitHub")]
    [Description("(Legacy) Resource Credentials for GitHub.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitHubCredentials,GitHub")]
    [PersistFrom("Inedo.Extensions.GitHub.Credentials.GitHubCredentials,GitHub")]
    public sealed class GitHubLegacyResourceCredentials : GitCredentialsBase
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
        public string OrganizationName { get; set; }

        [Persistent]
        [DisplayName("Repository")]
        [PlaceholderText("e.g. log4net")]
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

        internal static GitHubLegacyResourceCredentials TryCreate(string name, ValueEnumerationContext context)
        {
            return (GitHubLegacyResourceCredentials)ResourceCredentials.TryCreate(GitHubLegacyResourceCredentials.TypeName, name, environmentId: null, applicationId: context.ProjectId, inheritFromParent: false);
        }

        internal static GitHubLegacyResourceCredentials TryCreate(string name, IComponentConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitHubLegacyResourceCredentials)ResourceCredentials.TryCreate(GitHubLegacyResourceCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }
        internal static GitHubLegacyResourceCredentials TryCreate(string name, IOperationConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitHubLegacyResourceCredentials)ResourceCredentials.TryCreate(GitHubLegacyResourceCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }

        public override SecureCredentials ToSecureCredentials() => this.UserName == null ? null : new GitHubSecureCredentials
        {
            UserName = this.UserName,
            Password = this.Password
        };
        public override SecureResource ToSecureResource() => new GitHubSecureResource
        {
            ApiUrl = this.ApiUrl,
            OrganizationName = this.OrganizationName,
            RepositoryName = this.RepositoryName
        };
    }
}
