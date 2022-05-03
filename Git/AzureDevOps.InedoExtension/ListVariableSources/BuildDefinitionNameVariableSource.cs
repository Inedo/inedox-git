using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.ExecutionEngine;
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
    [DisplayName("Azure DevOps Build Definition")]
    [Description("Build configurations from a specified project in Azure DevOps.")]
    public sealed class BuildDefinitionNameVariableSource : DynamicListVariableType
    {
        [Persistent]
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        [Required]
        public string ResourceName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [Required]
        public string ProjectName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsSecureCredentials;
            if (resource == null)
                return Enumerable.Empty<string>();

            var projectName = AH.CoalesceString(this.ProjectName, resource.ProjectName);
            var api = new RestApi(credential?.Token, resource.InstanceUrl, null);

            var definitions = await api.GetBuildDefinitionsAsync(projectName).ConfigureAwait(false);

            return definitions.Select(d => d.name);
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not AzureDevOpsSecureResource resource || !Uri.TryCreate(resource.InstanceUrl.TrimEnd('/'), UriKind.Absolute, out _))
                return new LiteralHtml(value.AsString());

            return new A($"{resource.InstanceUrl.TrimEnd('/')}/{AH.CoalesceString(this.ProjectName, resource.ProjectName)}/{value.AsString()}/_build", value.AsString())
            {
                Class = "ci-icon azuredevops",
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
