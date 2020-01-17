using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Configurations;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Editors;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.Configurations
{
    [Serializable]
    [DisplayName("GitHub Milestone")]
    public sealed class GitHubMilestoneConfiguration : PersistedConfiguration, IExistential, IHasCredentials<GitHubCredentials>, IGitHubConfiguration
    {
        [ScriptAlias("From")]
        [DisplayName("From resource")]
        [SuggestableValue(typeof(GitHubSecureResourceSuggestionProvider))]
        public string ResourceName { get; set; }

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
        [Category("GitHub")]
        [ScriptAlias("Organization")]
        [DisplayName("Organization name")]
        [MappedCredential(nameof(GitHubCredentials.OrganizationName))]
        [PlaceholderText("Use organization from credentials")]
        [SuggestableValue(typeof(OrganizationNameSuggestionProvider))]
        public string OrganizationName { get; set; }

        [Persistent]
        [Category("GitHub")]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [MappedCredential(nameof(GitHubCredentials.RepositoryName))]
        [PlaceholderText("Use repository from credentials")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitHubClient.GitHubComUrl)]
        [Description("Leave this value blank to connect to github.com. For local installations of GitHub enterprise, an API URL must be specified.")]
        [MappedCredential(nameof(GitHubCredentials.ApiUrl))]
        public string ApiUrl { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Title")]
        public string Title { get; set; }

        [Persistent]
        [DisplayName("Due date")]
        [ScriptAlias("DueDate")]
        [CustomEditor(typeof(DateEditor))]
        public string DueDate { get; set; }

        [Persistent]
        [ScriptAlias("Description")]
        [FieldEditMode(FieldEditMode.Multiline)]
        public string Description { get; set; }

        public enum OpenOrClosed
        {
            open,
            closed
        }

        [Persistent]
        [ScriptAlias("State")]
        public OpenOrClosed? State { get; set; }

        [Persistent]
        public bool Exists { get; set; } = true;

        public override Task<ComparisonResult> CompareAsync(PersistedConfiguration other, IOperationCollectionContext context)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }
            if (!(other is GitHubMilestoneConfiguration c))
            {
                throw new InvalidOperationException("Cannot compare configurations of different types.");
            }

            return Task.FromResult(this.Compare(c));
        }

        private ComparisonResult Compare(GitHubMilestoneConfiguration other)
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

            if (!string.Equals(this.Title, other.Title))
            {
                differences.Add(new Difference(nameof(Title), this.Title, other.Title));
            }
            if (this.DueDate != null && !string.Equals(this.DueDate, other.DueDate ?? string.Empty))
            {
                differences.Add(new Difference(nameof(DueDate), this.DueDate, other.DueDate));
            }
            if (this.Description != null && !string.Equals(this.Description, other.Description ?? string.Empty))
            {
                differences.Add(new Difference(nameof(Description), this.Description, other.Description));
            }
            if (this.State.HasValue && this.State != other.State)
            {
                differences.Add(new Difference(nameof(State), this.State, other.State));
            }

            return new ComparisonResult(differences);
        }
    }
}
