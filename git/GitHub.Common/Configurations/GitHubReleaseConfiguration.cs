using Inedo.Documentation;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using System;
using System.Collections.Generic;
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
    [DisplayName("GitHub Release")]
    public sealed class GitHubReleaseConfiguration : PersistedConfiguration, IExistential, IHasCredentials<GitHubCredentials>
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

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(GitCredentialsBase.Password))]
        public SecureString Password { get; set; }

        [Persistent]
        [Category("GitHub")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        [PlaceholderText("Use organization from credentials")]
        [SuggestibleValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Persistent]
        [Category("GitHub")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        [PlaceholderText("Use repository from credentials")]
        [SuggestibleValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
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

        public override ComparisonResult Compare(PersistedConfiguration other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (!(other is GitHubReleaseConfiguration))
            {
                throw new InvalidOperationException("Cannot compare configurations of different types.");
            }

            return Compare((GitHubReleaseConfiguration)other);
        }

        private ComparisonResult Compare(GitHubReleaseConfiguration other)
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

            if (this.Target != null && !string.Equals(this.Target, other.Target))
            {
                differences.Add(new Difference(nameof(Target), this.Target, other.Target));
            }

            if (this.Title != null && !string.Equals(this.Title, other.Title))
            {
                differences.Add(new Difference(nameof(Title), this.Title, other.Title));
            }

            if (this.Description != null && !string.Equals(this.Description, other.Description))
            {
                differences.Add(new Difference(nameof(Description), this.Description, other.Description));
            }

            if (this.Draft.HasValue && this.Draft != other.Draft)
            {
                differences.Add(new Difference(nameof(Draft), this.Draft, other.Draft));
            }

            if (this.Prerelease.HasValue && this.Prerelease != other.Prerelease)
            {
                differences.Add(new Difference(nameof(Prerelease), this.Prerelease, other.Prerelease));
            }

            return new ComparisonResult(differences);
        }
    }
}
