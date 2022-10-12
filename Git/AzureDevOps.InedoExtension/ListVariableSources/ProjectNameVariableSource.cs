using System.ComponentModel;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensibility.VariableTemplates;
using Inedo.Serialization;
using Inedo.Web;
using Inedo.Web.Controls;
using Inedo.Web.Controls.SimpleHtml;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Project")]
    [Description("Projects from Azure DevOps.")]
    public sealed class ProjectNameVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsRepository>))]
        [Required]
        public string ResourceName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsRepository;
            if (resource == null || resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) is not AzureDevOpsAccount credential)
                return Enumerable.Empty<string>();

            using var client = new AzureDevOpsClient(AH.CoalesceString(resource.LegacyInstanceUrl, credential.ServiceUrl), credential.Password);
            return await client.GetProjectsAsync().ToListAsync().ConfigureAwait(false);
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

            return new A($"{url.AsSpan().TrimEnd('/')}/{value.AsString()}", value.AsString())
            {
                Classes = { "ci-icon", "azuredevops" },
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Azure DevOps (", new Hilite(this.ResourceName), ") ", " projects.");
        }
    }
}
