using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Plans;

namespace Inedo.Extensions.GitLab.Credentials
{
    [ScriptAlias(GitLabLegacyResourceCredentials.TypeName)]
    [DisplayName("GitLab")]
    [Description("(Legacy) Resource Credentials for GitLab.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitLabCredentials,GitLab")]
    [PersistFrom("Inedo.Extensions.GitLab.Credentials.GitLabCredentials,GitLab")]
    public sealed class GitLabLegacyResourceCredentials : GitCredentialsBase
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
        public string GroupName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [PlaceholderText("e.g. log4net")]
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

        internal static GitLabLegacyResourceCredentials TryCreate(string name, ValueEnumerationContext context)
        {
            return (GitLabLegacyResourceCredentials)ResourceCredentials.TryCreate(GitLabLegacyResourceCredentials.TypeName, name, environmentId: null, applicationId: context.ProjectId, inheritFromParent: false);
        }

        internal static GitLabLegacyResourceCredentials TryCreate(string name, IComponentConfiguration config)
        {
            int? projectId = (config.EditorContext as IOperationEditorContext)?.ProjectId ?? AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitLabLegacyResourceCredentials)ResourceCredentials.TryCreate(GitLabLegacyResourceCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }
        internal static GitLabLegacyResourceCredentials TryCreate(string name, IOperationConfiguration config)
        {
            int? projectId = AH.ParseInt(AH.CoalesceString(config["ProjectId"], config["ApplicationId"]));
            int? environmentId = AH.ParseInt(config["EnvironmentId"]);

            return (GitLabLegacyResourceCredentials)ResourceCredentials.TryCreate(GitLabLegacyResourceCredentials.TypeName, name, environmentId: environmentId, applicationId: projectId, inheritFromParent: false);
        }

        public override SecureResource ToSecureResource() => new GitLabSecureResource
        {
            ApiUrl = this.ApiUrl,
            GroupName = this.GroupName,
            ProjectName = this.ProjectName
        };
        public override SecureCredentials ToSecureCredentials() => string.IsNullOrEmpty(this.UserName) ? null : 
            new GitLabSecureCredentials
            {
                UserName = this.UserName,
                PersonalAccessToken = this.Password
            };
    }
}
