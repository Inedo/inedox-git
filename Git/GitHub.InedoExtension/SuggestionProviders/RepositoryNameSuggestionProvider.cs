using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class RepositoryNameSuggestionProvider : GitHubSuggestionProvider
    {
        internal override Task<IEnumerable<string>> GetSuggestionsAsync() => MakeAsync(this.Client.GetRepositoriesAsync(this.ComponentConfiguration[nameof(IGitHubConfiguration.OrganizationName)], CancellationToken.None));
    }
}
