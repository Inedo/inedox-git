using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class BuildDefinitionNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var definitions = await this.Client.GetBuildDefinitionsAsync(this.Resource.ProjectName).ConfigureAwait(false);
            return definitions.Select(d => d.name);
        }
    }
}
