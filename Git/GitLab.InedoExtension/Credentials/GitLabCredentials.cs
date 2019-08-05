using System;
using System.ComponentModel;
using System.Security;
using System.Threading;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Credentials
{
    [ScriptAlias("GitLab")]
    [DisplayName("GitLab")]
    [Description("Credentials for GitLab.")]
    [PersistFrom("Inedo.Extensions.Credentials.GitLabCredentials,GitLab")]
    public sealed class GitLabCredentials : GitCredentials
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
        public override string RepositoryUrl
        {
            get
            {
                var gitlab = new GitLabClient(this.ApiUrl, this.UserName, this.Password, this.GroupName);

                var project = gitlab.GetProjectAsync(this.ProjectName, CancellationToken.None).Result();

                if (project == null)
                    throw new InvalidOperationException($"Project '{this.ProjectName}' not found on GitLab.");

                return (string)project["http_url_to_repo"];
            }
            set
            {
            }
        }

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
