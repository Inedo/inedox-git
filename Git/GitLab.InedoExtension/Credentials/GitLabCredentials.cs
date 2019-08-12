using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Credentials
{
    [ScriptAlias(GitLabCredentials.TypeName)]
    [DisplayName("GitLab")]
    [Description("Credentials for GitLab.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitLabCredentials,GitLab")]
    public sealed class GitLabCredentials : GitCredentialsBase
    {
        public const string TypeName = "GitLab";

        [Persistent]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        public string ApiUrl { get; set; }

        [Persistent]
        [DisplayName("Group name")]
        [PlaceholderText("e.g. apache")]
        [SuggestableValue(typeof(CredentialsGroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [PlaceholderText("e.g. log4net")]
        [SuggestableValue(typeof(CredentialsProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Persistent]
        [Undisclosed]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override string RepositoryUrl { get; set; }

        [Persistent]
        [DisplayName("User name")]
        public override string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [DisplayName("Personal access token")]
        [FieldEditMode(FieldEditMode.Password)]
        public override SecureString Password { get; set; }

        public override RichDescription GetDescription()
        {
            var desc = new RichDescription(AH.CoalesceString(this.UserName, "Anonymous"), "@", "GitLab");
            if (!string.IsNullOrEmpty(this.GroupName))
                desc.AppendContent(",Group=", this.GroupName);

            return desc;            
        }

        internal static GitLabCredentials TryCreate(string name, ValueEnumerationContext context)
        {
            return (GitLabCredentials)ResourceCredentials.TryCreate(GitLabCredentials.TypeName, name, environmentId: null, applicationId: context.ProjectId, inheritFromParent: false);
        }

        internal static GitLabCredentials TryCreate(string name, IComponentConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitLabCredentials)ResourceCredentials.TryCreate(GitLabCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }
        internal static GitLabCredentials TryCreate(string name, IOperationConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitLabCredentials)ResourceCredentials.TryCreate(GitLabCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }
    }
}
