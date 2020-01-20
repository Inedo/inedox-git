using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class WorkItemTypeSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IAzureDevOpsConfiguration.ProjectName)], this.Resource?.ProjectName);

            if (string.IsNullOrEmpty(projectName))
                return Enumerable.Empty<string>();

            var types = await this.Client.GetWorkItemTypesAsync(projectName).ConfigureAwait(false);
            return from t in types
                   orderby t.name
                   select t.name;
        }
    }
}
