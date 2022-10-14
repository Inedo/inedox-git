using System.Runtime.CompilerServices;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class ProjectNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override async IAsyncEnumerable<string> GetSuggestionsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var p in this.Client.GetProjectsAsync(cancellationToken).ConfigureAwait(false))
                yield return p.Name;
        }
    }
}
