using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class ArtifactNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var projectName = AH.CoalesceString(config["Project"], config["ProjectName"]);
            var definitionName = config["BuildDefinition"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(definitionName))
                return Enumerable.Empty<string>();

            var buildNumber = config["BuildNumber"];

            var credentials = ResourceCredentials.Create<AzureDevOpsCredentials>(credentialName);

            var api = new RestApi(credentials, null);

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
