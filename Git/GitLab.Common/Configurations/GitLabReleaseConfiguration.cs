using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using System;
using System.ComponentModel;
using System.Security;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Web.Controls;
#endif

namespace Inedo.Extensions.Configurations
{
    [Serializable]
    [DisplayName("GitLab Release")]
    public sealed class GitLabReleaseConfiguration : PersistedConfiguration, IExistential, IHasCredentials<GitLabCredentials>
    {
        [Persistent]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
#if !BuildMaster
        [IgnoreConfigurationDrift]
#endif
        public string CredentialName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.UserName))]
#if !BuildMaster
        [IgnoreConfigurationDrift]
#endif
        public string UserName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.Password))]
#if !BuildMaster
        [IgnoreConfigurationDrift]
#endif
        public SecureString Password { get; set; }

        [Persistent]
        [Category("GitLab")]
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [MappedCredential(nameof(GitLabCredentials.GroupName))]
        [PlaceholderText("Use group from credentials")]
        [SuggestibleValue(typeof(GroupNameSuggestionProvider))]
#if !BuildMaster
        [IgnoreConfigurationDrift]
#endif
        public string GroupName { get; set; }

        [Persistent]
        [Category("GitLab")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [MappedCredential(nameof(GitLabCredentials.ProjectName))]
        [PlaceholderText("Use project from credentials")]
        [SuggestibleValue(typeof(ProjectNameSuggestionProvider))]
#if !BuildMaster
        [IgnoreConfigurationDrift]
#endif
        public string ProjectName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        [MappedCredential(nameof(GitLabCredentials.ApiUrl))]
#if !BuildMaster
        [IgnoreConfigurationDrift]
#endif
        public string ApiUrl { get; set; }

        [Persistent]
        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag name")]
        [Description("The tag must already exist.")]
        [ConfigurationKey]
        public string Tag { get; set; }

        [Persistent]
        [ScriptAlias("Description")]
        [DisplayName("Description")]
        [Description("Release notes, formatted using Markdown.")]
        public string Description { get; set; }

        [Persistent]
        public bool Exists { get; set; } = true;
    }
}