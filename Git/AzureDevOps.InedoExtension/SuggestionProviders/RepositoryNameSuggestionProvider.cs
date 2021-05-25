using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class RepositoryNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var repositories = await this.Client.GetRepositoriesAsync(this.Resource.ProjectName).ConfigureAwait(false);
            return repositories.Select(p => p.name);
        }
    }
}
