using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Serialization;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Project")]
    [Description("Projects from Azure DevOps.")]
    public sealed class ProjectNameVariableSource : ListVariableSource, IHasCredentials<AzureDevOpsCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        [Required]
        public string CredentialName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var credentials = ResourceCredentials.Create<AzureDevOpsCredentials>(this.CredentialName);

            var api = new RestApi(credentials, null);
            var projects = await api.GetProjectsAsync().ConfigureAwait(false);

            return projects.Select(p => p.name);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Azure DevOps (", new Hilite(this.CredentialName), ") ", " projects.");
        }
    }
}
