using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class OrganizationNameSuggestionProvider : GitHubSuggestionProvider
    {
        internal override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            if (this.Credentials == null)
                return Task.FromResult(Enumerable.Empty<string>());

            return MakeAsync(this.Client.GetOrganizationsAsync(CancellationToken.None));
        }
    }
}
