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
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.ListVariableSources
{
    [DisplayName("GitLab Branches")]
    [Description("Branches from a GitLab repository.")]
    public sealed class BranchListVariableSource : ListVariableSource, IHasCredentials<GitLabCredentials>
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
        [Category("GitLab")]
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [MappedCredential(nameof(GitLabCredentials.GroupName))]
        [PlaceholderText("Use group from credentials")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        public string GroupName { get; set; }

        [Persistent]
        [Category("GitLab")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [MappedCredential(nameof(GitLabCredentials.ProjectName))]
        [PlaceholderText("Use project from credentials")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        [MappedCredential(nameof(GitLabCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            this.SetValues();

            var client = new GitLabClient(this.ApiUrl, this.UserName, this.Password, this.GroupName);

            return await client.GetBranchesAsync(this.ProjectName, CancellationToken.None);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("GitLab (", new Hilite(this.ProjectName), ") branches.");
        }
    }
}
