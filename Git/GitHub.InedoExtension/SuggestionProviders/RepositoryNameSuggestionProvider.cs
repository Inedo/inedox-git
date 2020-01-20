using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class RepositoryNameSuggestionProvider : GitHubSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var repos = await this.Client.GetRepositoriesAsync(CancellationToken.None).ConfigureAwait(false);
            var names = from m in repos
                        let name = m["name"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;
            return names;
        }
    }
}
