using System.Runtime.CompilerServices;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class BuildDefinitionNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override async IAsyncEnumerable<string> GetSuggestionsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var d in this.Client.GetBuildDefinitionsAsync(this.Resource.ProjectName, cancellationToken).ConfigureAwait(false))
                yield return d.Name;
        }
    }
}
