using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Branches")]
    [Description("Branches from a Azure DevOps repository.")]
    public sealed class BranchListVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        [Required]
        public string ResourceName { get; set; }

        [Persistent]
        [ScriptAlias("Repository")]
        [DisplayName("Repository name")]
        [PlaceholderText("Use repository from Azure DevOps resource")]
        [SuggestableValue(typeof(RepositoryNameSuggestionProvider))]
        public string RepositoryName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsSecureCredentials;
            if (resource == null)
            {
                var rc = SecureCredentials.TryCreate(this.ResourceName, new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsCredentials;
                resource = (AzureDevOpsSecureResource)rc?.ToSecureResource();
                credential = (AzureDevOpsSecureCredentials)rc?.ToSecureCredentials();
            }
            if (resource == null)
                return Enumerable.Empty<string>();

            var api = new RestApi(credential?.Token, resource.InstanceUrl, null);

            var branches = await api.GetBranchesAsync(resource.ProjectName, AH.CoalesceString(this.RepositoryName, resource.RepositoryName)).ConfigureAwait(false);
            if (branches == null)
                return Enumerable.Empty<string>();

           return branches.Select(b => b.branchName);
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsSecureCredentials;
            if (resource == null)
            {
                var rc = SecureCredentials.TryCreate(this.ResourceName, new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsCredentials;
                resource = (AzureDevOpsSecureResource)rc?.ToSecureResource();
                credential = (AzureDevOpsSecureCredentials)rc?.ToSecureCredentials();
            }
            if (resource == null || !Uri.TryCreate(resource.InstanceUrl.TrimEnd('/'), UriKind.Absolute, out var parsedUri))
                return new LiteralHtml(value.AsString());

            // Ideally we would use the GitHubClient to retreive the proper URL, but that's resource intensive and we can guess the convention
            return new A($"{resource.InstanceUrl.TrimEnd('/')}/{resource.ProjectName}/_git/{AH.CoalesceString(this.RepositoryName, resource.RepositoryName)}?version=GB{value.AsString()}", value.AsString())
            {
                Class = "ci-icon azuredevops",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            var repoName = AH.CoalesceString(this.ResourceName, this.RepositoryName);
            return new RichDescription("GitHub (", new Hilite(repoName), ") branch.");
        }
    }
}
