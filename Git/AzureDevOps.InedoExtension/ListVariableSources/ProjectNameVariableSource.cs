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
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Project")]
    [Description("Projects from Azure DevOps.")]
    public sealed class ProjectNameVariableSource : ListVariableSource
    {
        [Persistent]
        [DisplayName("From AzureDevOps resource")]
        [SuggestableValue(typeof(SecureResourceSuggestionProvider<AzureDevOpsSecureResource>))]
        [Required]
        public string ResourceName { get; set; }

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

            var api = new RestApi(credential?.Token, resource.InstanceUrl, null);
            var projects = await api.GetProjectsAsync().ConfigureAwait(false);
            return projects.Select(p => p.name);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Azure DevOps (", new Hilite(this.ResourceName), ") ", " projects.");
        }
    }
}
