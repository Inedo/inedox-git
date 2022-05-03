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
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        [Required]
        public string ResourceName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateListValuesAsync(VariableTemplateContext context)
        {
            var resource = SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) as AzureDevOpsSecureResource;
            var credential = resource?.GetCredentials(new CredentialResolutionContext(context.ProjectId, null)) as AzureDevOpsSecureCredentials;
            if (resource == null)
                return Enumerable.Empty<string>();

            var api = new RestApi(credential?.Token, resource.InstanceUrl, null);
            var projects = await api.GetProjectsAsync().ConfigureAwait(false);
            return projects.Select(p => p.name);
        }

        public override ISimpleControl CreateRenderer(RuntimeValue value, VariableTemplateContext context)
        {
            if (SecureResource.TryCreate(this.ResourceName, new ResourceResolutionContext(context.ProjectId)) is not AzureDevOpsSecureResource resource || !Uri.TryCreate(resource.InstanceUrl.TrimEnd('/'), UriKind.Absolute, out _))
                return new LiteralHtml(value.AsString());

            return new A($"{resource.InstanceUrl.TrimEnd('/')}/{value.AsString()}", value.AsString())
            {
                Class = "ci-icon azuredevops",
                Target = "_blank"
            };
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Azure DevOps (", new Hilite(this.ResourceName), ") ", " projects.");
        }
    }
}
