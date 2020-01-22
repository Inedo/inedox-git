using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.ListVariableSources
{
    [DisplayName("GitLab Branches")]
    [Description("Branches from a GitLab repository.")]
    public sealed class BranchListVariableSource : ListVariableSource, IMissingPersistentPropertyHandler
    {
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabSecureResource>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Project")]
        [DisplayName("Project name")]
        [PlaceholderText("Use project from resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        void IMissingPersistentPropertyHandler.OnDeserializedMissingProperties(IReadOnlyDictionary<string, string> missingProperties)
        {
            if (string.IsNullOrEmpty(this.ResourceName) && missingProperties.TryGetValue("CredentialName", out var value))
                this.ResourceName = value;
        }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitLabSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as GitLabSecureCredentials;
            if (resource == null)
            {
                var rc = SecureCredentials.TryCreate(this.ResourceName, new CredentialResolutionContext(context.ProjectId, null)) as GitLabLegacyResourceCredentials;
                resource = (GitLabSecureResource)rc?.ToSecureResource();
                credential = (GitLabSecureCredentials)rc?.ToSecureCredentials();
            }
            if (resource == null)
                throw new InvalidOperationException($"Could not find resource \"{this.ResourceName}\".");

            var client = new GitLabClient(resource.ApiUrl, credential?.UserName, credential?.PersonalAccessToken, resource.GroupName);

            return await client.GetBranchesAsync(this.ProjectName, CancellationToken.None).ConfigureAwait(false);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("GitLab (", new Hilite(this.ProjectName), ") branches.");
        }
    }
}
