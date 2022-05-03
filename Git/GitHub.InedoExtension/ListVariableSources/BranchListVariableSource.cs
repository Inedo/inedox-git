using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Extensions.GitHub.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.GitHub.ListVariableSources
{
    [DisplayName("GitHub Branches")]
    [Description("Branches from a GitHub repository.")]
    public sealed class BranchListVariableSource : DynamicListVariableType, IMissingPersistentPropertyHandler
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
        public override Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as GitHubSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as GitHubSecureCredentials;
            if (resource == null)
                return Task.FromResult(Enumerable.Empty<string>());

            var client = new GitHubClient(resource.ApiUrl, credential?.UserName, credential?.Password, resource.OrganizationName);

            return client.ListRefsAsync(resource.OrganizationName, resource.RepositoryName, RefType.Branch, CancellationToken.None);
        }
        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not GitHubSecureResource resource || !Uri.TryCreate(AH.CoalesceString(resource.ApiUrl, GitHubClient.GitHubComUrl).TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            var hostName = parsedUri.Host == "api.github.com" ? "github.com" : parsedUri.Host;
            return new A($"https://{hostName}/{resource.OrganizationName}/{resource.RepositoryName}/tree/{value.AsString()}", value.AsString()) 
            { 
                Class = "ci-icon github",
                Target = "_blank"
            };
        }
        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.ResourceName, this.RepositoryName);
            return new RichDescription("GitHub (", new Hilite(repoName), ") branches.");
        }
    }
}
