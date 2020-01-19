using Inedo.Extensibility;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Web;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class RepositoryNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var (credentials, resource) = config.GetCredentialsAndResource();
            if (resource == null || credentials == null)
                return Enumerable.Empty<string>();

            var client = new GitHubClient(resource.ApiUrl, credentials?.UserName, credentials?.Password, resource.OrganizationName);

            var repos = await client.GetRepositoriesAsync(CancellationToken.None).ConfigureAwait(false);
            var names = from m in repos
                        let name = m["name"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;
            return names;
        }
    }
}
