using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Configurations
{
    [Serializable]
    [DisplayName("GitHub Release")]
    public sealed class GitHubReleaseConfiguration : PersistedConfiguration, IExistential, IGitHubConfiguration, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [ScriptAlias("From")]
        [ScriptAlias("Credentials")]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitHubRepository>))]
        [IgnoreConfigurationDrift]
        public string ResourceName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from GitHub resource's credentials")]
        [IgnoreConfigurationDrift]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from GitHub resource's credentials")]
        [IgnoreConfigurationDrift]
        public SecureString Password { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [PlaceholderText("Use organization from Github resource")]
        [SuggestableValue(typeof(OrganizationNameSuggestionProvider))]
        [IgnoreConfigurationDrift]
        public string OrganizationName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Github resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        [IgnoreConfigurationDrift]
        public string RepositoryName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText("Use URL from Github resource.")]
        [IgnoreConfigurationDrift]
        public string ApiUrl { get; set; }

        [Persistent]
        [Required]
        [ScriptAlias("Tag")]
        [DisplayName("Tag name")]
        [ConfigurationKey]
        public string Tag { get; set; }

        [Persistent]
        [ScriptAlias("Target")]
        [DisplayName("Target commit")]
        [Description("May be specified as a branch name, a commit hash, or left blank for the latest commit on the default branch (usually master).")]
        public string Target { get; set; }

        [Persistent]
        [ScriptAlias("Title")]
        [DisplayName("Title")]
        [Description("If left blank, the tag name will be used for new releases and existing releases will keep their original title.")]
        [PlaceholderText("(keep existing)")]
        public string Title { get; set; }

        [Persistent]
        [ScriptAlias("Description")]
        [DisplayName("Description")]
        [Description("Release notes, formatted as Markdown. Leave blank to keep the existing release notes.")]
        [PlaceholderText("(keep existing)")]
        public string Description { get; set; }

        [Persistent]
        [ScriptAlias("Draft")]
        [DisplayName("Is draft")]
        [PlaceholderText("(keep existing)")]
        public bool? Draft { get; set; }

        [Persistent]
        [ScriptAlias("Prerelease")]
        [DisplayName("Is prerelease")]
        [PlaceholderText("(keep existing)")]
        public bool? Prerelease { get; set; }

        [Persistent]
        public bool Exists { get; set; } = true;

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            if (other is not GitHubReleaseConfiguration c)
                throw new InvalidOperationException("Cannot compare configurations of different types.");

            return Task.FromResult(this.Compare(c));
        }

        private ComparisonResult Compare(GitHubReleaseConfiguration other)
        {
            if (!this.Exists && !other.Exists)
                return ComparisonResult.Identical;

            if (!this.Exists || !other.Exists)
                return new ComparisonResult(new[] { new Difference(nameof(Exists), this.Exists, other.Exists) });

            var differences = new List<Difference>();

            if (!string.Equals(this.Tag, other.Tag, StringComparison.OrdinalIgnoreCase))
                differences.Add(new Difference(nameof(Tag), this.Tag, other.Tag));

            if (this.Target != null && !string.Equals(this.Target, other.Target))
                differences.Add(new Difference(nameof(Target), this.Target, other.Target));

            if (this.Title != null && !string.Equals(this.Title, other.Title))
                differences.Add(new Difference(nameof(Title), this.Title, other.Title));

            if (this.Description != null && !string.Equals(this.Description, other.Description))
                differences.Add(new Difference(nameof(Description), this.Description, other.Description));

            if (this.Draft.HasValue && this.Draft != other.Draft)
                differences.Add(new Difference(nameof(Draft), this.Draft, other.Draft));

            if (this.Prerelease.HasValue && this.Prerelease != other.Prerelease)
                differences.Add(new Difference(nameof(Prerelease), this.Prerelease, other.Prerelease));

            return new ComparisonResult(differences);
        }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (string.IsNullOrEmpty(this.ResourceName) && missingProperties.TryGetValue("CredentialName", out var value))
                this.ResourceName = value;
        }
    }
}
