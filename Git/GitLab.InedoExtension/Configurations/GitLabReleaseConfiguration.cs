using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Configurations
{
    [Serializable]
    [DisplayName("GitLab Release")]
    public sealed class GitLabReleaseConfiguration : PersistedConfiguration, IExistential, IGitLabConfiguration, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitLab resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabRepository>))]
        [IgnoreConfigurationDrift]
        public string ResourceName { get; set; }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (string.IsNullOrEmpty(this.ResourceName) && missingProperties.TryGetValue("CredentialName", out var value))
                this.ResourceName = value;
        }


        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitLab resource's credentials")]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitLab resource's credentials")]
        public SecureString Password { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("Namespace")]
        [ScriptAlias("Group", Obsolete = true)]
        [DisplayName("Namespace")]
        [PlaceholderText("Use namespace from GitLab resource")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        [IgnoreConfigurationDrift]
        public string GroupName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [PlaceholderText("Use project from GitLab resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [IgnoreConfigurationDrift]
        public string ProjectName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText("Use URL from GitLab resource")]
        [IgnoreConfigurationDrift]
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

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (!(other is GitLabReleaseConfiguration c))
            {
                throw new InvalidOperationException("Cannot compare configurations of different types.");
            }

            return Task.FromResult(this.Compare(c));
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
    }
}