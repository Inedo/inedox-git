using System.Runtime.CompilerServices;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class RepositoryNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override async IAsyncEnumerable<string> GetSuggestionsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(this.Resource?.ProjectName))
                yield break;

            await foreach (var r in this.Client.GetRepositoriesAsync(this.Resource.ProjectName, cancellationToken))
                yield return r.Name;
        }
    }
}
