using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class WorkItemTypeSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            if (string.IsNullOrEmpty(this.Resource.ProjectName))
                return Enumerable.Empty<string>();

            var types = await this.Client.GetWorkItemTypesAsync(this.Resource.ProjectName).ConfigureAwait(false);
            return from t in types
                   orderby t.name
                   select t.name;
        }
    }
}
