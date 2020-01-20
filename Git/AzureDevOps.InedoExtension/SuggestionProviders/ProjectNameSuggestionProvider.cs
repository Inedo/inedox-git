using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class ProjectNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var projects = await this.Client.GetProjectsAsync().ConfigureAwait(false);
            return projects.Select(p => p.name);
        }
    }
}
