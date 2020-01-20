using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class ArtifactNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {        
        internal override async Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IAzureDevOpsConfiguration.ProjectName)], this.Resource?.ProjectName);
            var definitionName = this.ComponentConfiguration["BuildDefinition"];
            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(definitionName))
                return Enumerable.Empty<string>();

            var buildNumber = this.ComponentConfiguration["BuildNumber"];
            var api = this.Client;

            var definition = await api.GetBuildDefinitionAsync(projectName, definitionName).ConfigureAwait(false);
            if (definition == null)
                return Enumerable.Empty<string>();

            var builds = await api.GetBuildsAsync(
                project: projectName,
                buildDefinition: definition.id,
                buildNumber: AH.NullIf(buildNumber, ""),
                resultFilter: "succeeded",
                statusFilter: "completed",
                top: 2
            ).ConfigureAwait(false);

            var build = builds.FirstOrDefault();

            if (build == null)
                return Enumerable.Empty<string>();

            var artifacts = await api.GetArtifactsAsync(projectName, build.id).ConfigureAwait(false);

            return artifacts.Select(a => a.name);
        }
    }
}
