using System;
using System.ComponentModel;
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

namespace Inedo.Extensions.GitLab.VariableTemplates
{
    [DisplayName("[Obsolete] GitLab CommitId")]
    [Description("CommitId within a GitLab repository.")]
    [Undisclosed]
    public sealed class CommitIdVariable : VariableTemplateType
    {
        [Persistent]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitLabRepository>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use Project Name from GitLab resource")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        public string ProjectName { get; set; }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not GitLabRepository resource 
                || !Uri.TryCreate(AH.CoalesceString(resource.LegacyApiUrl, GitLabClient.GitLabComUrl).TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            var hostName = parsedUri.Host == "api.gitlab.com" ? "gitlab.com" : parsedUri.Host;
            return new A($"https://{hostName}/{resource.GroupName}/{AH.CoalesceString(this.ProjectName, resource.ProjectName)}/-/commit/{value.AsString()}", value.AsString()[..7])
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
