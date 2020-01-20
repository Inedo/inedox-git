using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Build Definition")]
    [Description("Build configurations from a specified project in Azure DevOps.")]
    public sealed class BuildDefinitionNameVariableSource : ListVariableSource
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

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
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

            var projectName = AH.CoalesceString(this.ProjectName, resource.ProjectName);
            var api = new RestApi(credential?.Token, resource.InstanceUrl, null);

            var definitions = await api.GetBuildDefinitionsAsync(projectName).ConfigureAwait(false);

            return definitions.Select(d => d.name);
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
