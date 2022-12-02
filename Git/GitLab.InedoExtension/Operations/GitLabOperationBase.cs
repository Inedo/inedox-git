using System.ComponentModel;
using System.Security;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Operations
{
    public abstract class GitLabOperationBase : ExecuteOperation, IGitLabConfiguration
    {
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitLab resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabRepository>))]
        public string ResourceName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitLab resource's credentials")]
        public string UserName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitLab resource's credentials")]
        public SecureString Password { get; set; }


        [Category("Connection/Identity")]
        [ScriptAlias("Namespace")]
        [ScriptAlias("Group", Obsolete = true)]
        [DisplayName("Namespace")]
        [PlaceholderText("Use namespace from GitLab resource")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [PlaceholderText("Use project from GitLab resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText("Use URL from GitLab resource")]
        public string ApiUrl { get; set; }
    }
}