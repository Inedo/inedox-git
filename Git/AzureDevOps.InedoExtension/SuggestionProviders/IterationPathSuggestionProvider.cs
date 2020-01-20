using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class IterationPathSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IAzureDevOpsConfiguration.ProjectName)], this.Resource?.ProjectName);

            if (string.IsNullOrEmpty(projectName))
                return Enumerable.Empty<string>();

            var iterations = await this.Client.GetIterationsAsync(projectName).ConfigureAwait(false);
            return iterations.Select(i => i.path);
        }
    }
}
