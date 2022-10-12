using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Build Definition")]
    [Description("Build configurations from a specified project in Azure DevOps.")]
    public sealed class BuildDefinitionNameVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsRepository>))]
        [Required]
        public string ResourceName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [Required]
        public string ProjectName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsRepository;
            if (resource == null || resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) is not AzureDevOpsAccount credential)
                return Enumerable.Empty<string>();

            var projectName = AH.CoalesceString(this.ProjectName, resource.ProjectName);
            using var client = new AzureDevOpsClient(AH.CoalesceString(resource.LegacyInstanceUrl, credential.ServiceUrl), credential.Password);
            return await client.GetBuildDefinitionsAsync(projectName).ToListAsync().ConfigureAwait(false);
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

            return new A($"{url.AsSpan().TrimEnd('/')}/{AH.CoalesceString(this.ProjectName, resource.ProjectName)}/{value.AsString()}/_build", value.AsString())
            {
                Classes = { "ci-icon", "azuredevops" },
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            var description = new RichDescription("Azure DevOps (", new Hilite(this.ResourceName), ") ", " build definitions");
            if (!string.IsNullOrEmpty(this.ProjectName))
                description.AppendContent(" in ", new Hilite(this.ProjectName));
            description.AppendContent(".");
            return description;
        }
    }
}
