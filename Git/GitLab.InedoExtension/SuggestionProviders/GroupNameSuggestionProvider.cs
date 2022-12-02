using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class GroupNameSuggestionProvider : GitLabSuggestionProvider
    {
        private protected override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            if (this.Credentials == null)
                return Task.FromResult(Enumerable.Empty<string>());

            return MakeAsync(this.Client.GetGroupsAsync(CancellationToken.None));
        }
    }
}
