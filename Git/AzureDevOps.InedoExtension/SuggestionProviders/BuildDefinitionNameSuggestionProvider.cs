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
    internal sealed class BuildDefinitionNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var projectName = AH.CoalesceString(config["Project"], config["ProjectName"]);
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(projectName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<AzureDevOpsCredentials>(credentialName);

            var api = new RestApi(credentials, null);
            var definitions = await api.GetBuildDefinitionsAsync(projectName).ConfigureAwait(false);

            return definitions.Select(d => d.name);
        }
    }
}
