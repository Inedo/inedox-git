using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.GitLab.ListVariableSources
{
    [DisplayName("GitLab Branches")]
    [Description("Branches from a GitLab repository.")]
    public sealed class BranchListVariableSource : DynamicListVariableType, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [ScriptAlias("From")]
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

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitLabSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as GitLabSecureCredentials;
            if (resource == null)
                throw new InvalidOperationException($"Could not find resource \"{this.ResourceName}\".");

            var client = new GitLabClient(resource.ApiUrl, credential?.UserName, credential?.PersonalAccessToken, resource.GroupName);

            return await client.GetBranchesAsync(this.ProjectName, CancellationToken.None).ConfigureAwait(false);
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not GitLabSecureResource resource || !Uri.TryCreate(AH.CoalesceString(resource.ApiUrl, GitLabClient.GitLabComUrl).TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            var hostName = parsedUri.Host == "api.gitlab.com" ? "gitlab.com" : parsedUri.Host;
            return new A($"https://{hostName}/{resource.GroupName}/{resource.ProjectName}/-/tree/{value.AsString()}", value.AsString())
            {
                Class = "ci-icon gitlab",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("GitLab (", new Hilite(AH.CoalesceString(this.ProjectName, this.ResourceName)), ") branches.");
        }
    }
}
