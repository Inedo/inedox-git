using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.AzureDevOps.Client;
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
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsRepository>))]
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
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsRepository;
            if (resource == null || resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) is not AzureDevOpsAccount credential)
                return Enumerable.Empty<string>();

            var client = new AzureDevOpsClient(AH.CoalesceString(resource.LegacyInstanceUrl, credential.ServiceUrl), credential.Password);
            return (await client.GetBranchesAsync(resource.ProjectName, AH.CoalesceString(this.RepositoryName, resource.RepositoryName))
                .ToListAsync().ConfigureAwait(false))
                .Select(b => b.Name);
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not AzureDevOpsRepository resource)
                return new LiteralHtml(value.AsString());

            if (resource.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) is not AzureDevOpsAccount credential)
                return new LiteralHtml(value.AsString());

            var url = AH.CoalesceString(resource.LegacyInstanceUrl, credential.ServiceUrl);
            if (string.IsNullOrEmpty(url))
                return new LiteralHtml(value.AsString());

            return new A($"{url.AsSpan().TrimEnd('/')}/{resource.ProjectName}/_git/{AH.CoalesceString(this.RepositoryName, resource.RepositoryName)}?version=GB{value.AsString()}", value.AsString())
            {
                Classes = { "ci-icon", "azuredevops" },
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
