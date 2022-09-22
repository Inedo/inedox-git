using System;
using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.GitHub.VariableTemplates
{
    [DisplayName("[Obsolete] GitHub CommitId")]
    [Description("CommitId within a GitHub repository.")]
    [Undisclosed]
    public sealed class CommitIdVariable : VariableTemplateType
    {
        [Persistent]
        [DisplayName("From GitHub resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<GitHubRepository>))]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Github resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not GitHubRepository resource 
                || !Uri.TryCreate(AH.CoalesceString(resource.LegacyApiUrl, GitHubClient.GitHubComUrl).TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            var hostName = parsedUri.Host == "api.github.com" ? "github.com" : parsedUri.Host;
            return new A($"https://{hostName}/{resource.OrganizationName}/{AH.CoalesceString(this.RepositoryName, resource.RepositoryName)}/commit/{value.AsString()}", value.AsString()[..7])
            {
                Class = "ci-icon github",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.ResourceName, this.RepositoryName);
            return new RichDescription("GitHub (", new Hilite(repoName), ") commit.");
        }
    }
}
