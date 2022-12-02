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
using Inedo.Extensions.GitLab.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.GitLab.ListVariableSources
{
    [DisplayName("[Obsolete] GitLab Branches")]
    [Description("Branches from a GitLab repository.")]
    [Undisclosed]
    public sealed class BranchListVariableSource : DynamicListVariableType, IMissingPersistentPropertyHandler
    {
        [Persistent]
        [ScriptAlias("From")]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabRepository>))]
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
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitLabRepository;
            if (resource == null)
                throw new InvalidOperationException($"Could not find resource \"{this.ResourceName}\".");

            var client = new GitLabClient(resource, new CredentialResolutionContext(context.ProjectId));

            var branches = new List<string>();
            await foreach (var b in client.GetBranchesAsync(resource, CancellationToken.None).ConfigureAwait(false))
                branches.Add(b.Name);

            return branches;
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not GitLabRepository resource 
                || !Uri.TryCreate(AH.CoalesceString(resource.LegacyApiUrl, GitLabClient.GitLabComUrl).TrimEnd('/'), UriKind.Absolute, out var parsedUri))
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
