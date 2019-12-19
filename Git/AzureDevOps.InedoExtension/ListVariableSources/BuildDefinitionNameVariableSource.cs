using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Documentation;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.ListVariableSources;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Extensions.AzureDevOps.SuggestionProviders;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.ListVariableSources
{
    [DisplayName("Azure DevOps Build Definition")]
    [Description("Build configurations from a specified project in Azure DevOps.")]
    public sealed class BuildDefinitionNameVariableSource : ListVariableSource, IHasCredentials<AzureDevOpsCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        [TriggerPostBackOnChange]
        [Required]
        public string CredentialName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [Required]
        public string ProjectName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var credentials = ResourceCredentials.Create<AzureDevOpsCredentials>(this.CredentialName);

            var api = new RestApi(credentials, null);
            var definitions = await api.GetBuildDefinitionsAsync(this.ProjectName).ConfigureAwait(false);

            return definitions.Select(d => d.name);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Azure DevOps (", new Hilite(this.CredentialName), ") ", " build definitions in ", new Hilite(this.ProjectName), ".");
        }
    }
}
