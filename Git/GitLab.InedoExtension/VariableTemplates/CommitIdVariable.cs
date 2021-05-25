using System;
using System.ComponentModel;
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

namespace Inedo.Extensions.GitLab.VariableTemplates
{
    [DisplayName("GitLab CommitId")]
    [Description("CommitId within a GitLab repository.")]
    public sealed class CommitIdVariable : VariableTemplateType
    {
        [Persistent]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabSecureResource>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use Project Name from GitLab resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitLabSecureResource;
            if (resource == null)
            {
                var rc = SecureCredentials.TryCreate(this.ResourceName, new CredentialResolutionContext(context.ProjectId, null)) as GitLabLegacyResourceCredentials;
                resource = (GitLabSecureResource)rc?.ToSecureResource();
            }
            if (resource == null || !Uri.TryCreate(AH.CoalesceString(resource.ApiUrl, GitLabClient.GitLabComUrl).TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            var hostName = parsedUri.Host == "api.gitlab.com" ? "gitlab.com" : parsedUri.Host;
            return new A($"https://{hostName}/{resource.GroupName}/{AH.CoalesceString(this.ProjectName, resource.ProjectName)}/-/commit/{value.AsString()}", value.AsString().Substring(0, 7))
            {
                Class = "ci-icon gitlab",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.ResourceName, this.ProjectName);
            return new RichDescription("GitLab (", new Hilite(repoName), ") commit.");
        }
    }
}
