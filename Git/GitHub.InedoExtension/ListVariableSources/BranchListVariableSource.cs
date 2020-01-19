using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitHub.ListVariableSources
{
    [DisplayName("GitHub Branches")]
    [Description("Branches from a GitHub repository.")]
    public sealed class BranchListVariableSource : ListVariableSource, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitHubSecureResource>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Github resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (string.IsNullOrEmpty(this.ResourceName) && missingProperties.TryGetValue("CredentialName", out var value))
                this.ResourceName = value;
        }
        public override Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitHubSecureResource;
            if (resource == null)
                throw new InvalidOperationException($"Could not find resource \"{this.ResourceName}\".");

            var credential = resource.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as GitHubSecureCredentials;
            var client = new GitHubClient(resource.ApiUrl, credential?.UserName, credential?.Password, resource.OrganizationName);

            return client.ListRefsAsync(resource.OrganizationName, resource.RepositoryName, RefType.Branch, CancellationToken.None);
        }

        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.ResourceName, this.RepositoryName);
            return new RichDescription("GitHub (", new Hilite(repoName), ") branches.");
        }
    }
}
