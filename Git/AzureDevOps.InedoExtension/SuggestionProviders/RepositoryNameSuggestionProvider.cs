namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class RepositoryNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(this.Resource?.ProjectName))
                return Enumerable.Empty<string>().ToAsyncEnumerable();

            return this.Client.GetRepositoryNamesAsync(this.Resource.ProjectName, cancellationToken);
        }
    }
}
