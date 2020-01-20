using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class OrganizationNameSuggestionProvider : GitHubSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            if (this.Credentials == null)
                return Enumerable.Empty<string>();

            var orgs = await this.Client.GetOrganizationsAsync(CancellationToken.None).ConfigureAwait(false);
            var names = from m in orgs
                        let name = m["login"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }
}
