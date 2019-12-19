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
    [DisplayName("Azure DevOps Build Number")]
    [Description("Build numbers from a specified build definition in Azure DevOps.")]
    public sealed class AzureDevOpsBuildNumberVariableSource : ListVariableSource, IHasCredentials<AzureDevOpsCredentials>
    {
        [Persistent]
        [DisplayName("Credentials")]
        [TriggerPostBackOnChange]
        [Required]
        public string CredentialName { get; set; }

        [Persistent]
        [DisplayName("Project")]
        [SuggestableValue(typeof(ProjectNameSuggestionProvider))]
        [TriggerPostBackOnChange]
        [Required]
        public string ProjectName { get; set; }

        [Persistent]
        [DisplayName("Build definition")]
        [SuggestableValue(typeof(BuildDefinitionNameSuggestionProvider))]
        [Required]
        public string BuildDefinitionName { get; set; }

        public override async Task<IEnumerable<string>> EnumerateValuesAsync(ValueEnumerationContext context)
        {
            var credentials = ResourceCredentials.Create<AzureDevOpsCredentials>(this.CredentialName);

            var api = new RestApi(credentials, null);
            var definition = await api.GetBuildDefinitionAsync(this.ProjectName, this.BuildDefinitionName).ConfigureAwait(false);
            if (definition == null)
                return Enumerable.Empty<string>();

            var builds = await api.GetBuildsAsync(this.ProjectName, definition.id).ConfigureAwait(false);
            return builds.Select(b => b.buildNumber);
        }

        public override RichDescription GetDescription()
        {
            return new RichDescription("Azure DevOps (", new Hilite(this.CredentialName), ") ", " builds for ", new Hilite(this.BuildDefinitionName), " in ", new Hilite(this.ProjectName), ".");
        }
    }
}
