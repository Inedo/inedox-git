using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class ProjectNameSuggestionProvider : GitLabSuggestionProvider
    {
        private protected override Task<IEnumerable<string>> GetSuggestionsAsync() => MakeAsync(this.Client.GetProjectsAsync(CancellationToken.None));
    }
}
