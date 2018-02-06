using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using System;
using System.ComponentModel;
using System.Security;
using System.Collections.Generic;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Configurations;
using Inedo.BuildMaster.Extensibility.Credentials;
using SuggestableValueAttribute = Inedo.BuildMaster.Web.Controls.SuggestableValueAttribute;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Configurations;
using Inedo.Otter.Extensibility.Credentials;
using SuggestableValueAttribute = Inedo.Otter.Web.Controls.SuggestableValueAttribute;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Web;
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
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
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
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
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

#if !BuildMaster
        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (!(other is GitLabReleaseConfiguration))
            {
                throw new InvalidOperationException("Cannot compare configurations of different types.");
            }

            return Compare((GitLabReleaseConfiguration)other);
        }

        private ComparisonResult Compare(GitLabReleaseConfiguration other)
        {
            if (!this.Exists && !other.Exists)
            {
                return ComparisonResult.Identical;
            }
            if (!this.Exists || !other.Exists)
            {
                return new ComparisonResult(new[] { new Difference(nameof(Exists), this.Exists, other.Exists) });
            }

            var differences = new List<Difference>();

            if (!string.Equals(this.Tag, other.Tag, StringComparison.OrdinalIgnoreCase))
            {
                differences.Add(new Difference(nameof(Tag), this.Tag, other.Tag));
            }

            if (this.Description != null && !string.Equals(this.Description, other.Description))
            {
                differences.Add(new Difference(nameof(Description), this.Description, other.Description));
            }

            return new ComparisonResult(differences);
        }
#endif
    }
}