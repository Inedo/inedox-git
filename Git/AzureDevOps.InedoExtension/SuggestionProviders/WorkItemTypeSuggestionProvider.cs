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
    internal sealed class WorkItemTypeSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];
            var project = config["Project"];
            if (string.IsNullOrEmpty(credentialName) || string.IsNullOrEmpty(project))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<AzureDevOpsCredentials>(credentialName);

            var api = new RestApi(credentials, null);
            var types = await api.GetWorkItemTypesAsync(project).ConfigureAwait(false);

            return from t in types
                   orderby t.name
                   select t.name;
        }
    }
}
