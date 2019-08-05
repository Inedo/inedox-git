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
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.Configurations
{
    [Serializable]
    [DisplayName("GitLab Milestone")]
    public sealed class GitLabMilestoneConfiguration : PersistedConfiguration, IExistential, IHasCredentials<GitLabCredentials>
    {
        [Persistent]
        [ScriptAlias("Credentials")]
        [DisplayName("Credentials")]
        [IgnoreConfigurationDrift]
        public string CredentialName { get; set; }

        [Persistent]
        [Category("Connection/Identity")]
        [ScriptAlias("UserName")]
        [DisplayName("User name")]
        [PlaceholderText("Use user name from credentials")]
        [MappedCredential(nameof(GitCredentials.UserName))]
        [IgnoreConfigurationDrift]
        public string UserName { get; set; }

        [Persistent(Encrypted = true)]
        [Category("Connection/Identity")]
        [ScriptAlias("Password")]
        [DisplayName("Password")]
        [PlaceholderText("Use password from credentials")]
        [MappedCredential(nameof(GitCredentials.Password))]
        [IgnoreConfigurationDrift]
        public SecureString Password { get; set; }

        [Persistent]
        [Category("GitLab")]
        [ScriptAlias("Group")]
        [DisplayName("Group name")]
        [MappedCredential(nameof(GitLabCredentials.GroupName))]
        [PlaceholderText("Use group from credentials")]
        [SuggestableValue(typeof(GroupNameSuggestionProvider))]
        [IgnoreConfigurationDrift]
        public string GroupName { get; set; }

        [Persistent]
        [Category("GitLab")]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [MappedCredential(nameof(GitLabCredentials.ProjectName))]
        [PlaceholderText("Use project from credentials")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [IgnoreConfigurationDrift]
        public string ProjectName { get; set; }

        [Persistent]
        [Category("Advanced")]
        [ScriptAlias("ApiUrl")]
        [DisplayName("API URL")]
        [PlaceholderText(GitLabClient.GitLabComUrl)]
        [Description("Leave this value blank to connect to gitlab.com. For local installations of GitLab, an API URL must be specified.")]
        [MappedCredential(nameof(GitLabCredentials.ApiUrl))]
        [IgnoreConfigurationDrift]
        public string ApiUrl { get; set; }

        [Required]
        [Persistent]
        [ScriptAlias("Title")]
        public string Title { get; set; }

        [Persistent]
        [DisplayName("Start date")]
        [ScriptAlias("StartDate")]
        [CustomEditor(typeof(DateEditor))]
        public string StartDate { get; set; }

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
            if (!(other is GitLabMilestoneConfiguration c))
            {
                throw new InvalidOperationException("Cannot compare configurations of different types.");
            }

            return Task.FromResult(this.Compare(c));
        }

        private ComparisonResult Compare(GitLabMilestoneConfiguration other)
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
            if (this.StartDate != null && !string.Equals(this.StartDate, other.StartDate ?? string.Empty))
            {
                differences.Add(new Difference(nameof(StartDate), this.StartDate, other.StartDate));
            }
            if (this.DueDate != null && !string.Equals(this.DueDate, other.DueDate ?? string.Empty))
            {
                differences.Add(new Difference(nameof(DueDate), this.DueDate, other.DueDate));
            }
            if (this.Description != null && !string.Equals(this.Description, other.Description))
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
