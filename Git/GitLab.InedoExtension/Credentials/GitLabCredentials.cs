using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Web;
using Inedo.BuildMaster.Web.Controls;
using SuggestableValueAttribute = Inedo.BuildMaster.Web.Controls.SuggestableValueAttribute;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Web.Controls;
using Inedo.Otter.Extensions;
using SuggestableValueAttribute = Inedo.Otter.Web.Controls.SuggestableValueAttribute;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Web;
#endif

namespace Inedo.Extensions.Credentials
{
    [ScriptAlias("GitLab")]
    [DisplayName("GitLab")]
    [Description("Credentials for GitLab.")]
    public sealed class GitLabCredentials : GitCredentialsBase
    {
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
    }
}
