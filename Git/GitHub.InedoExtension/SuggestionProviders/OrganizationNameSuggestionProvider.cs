using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class OrganizationNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var (credentials, resource) = config.GetCredentialsAndResource();
            if (resource == null)
                return Enumerable.Empty<string>();

            var client = new GitHubClient(resource.ApiUrl, credentials?.UserName, credentials?.Password, resource.OrganizationName);
            var orgs = await client.GetOrganizationsAsync(CancellationToken.None).ConfigureAwait(false);
            var names = from m in orgs
                        let name = m["login"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }
}
