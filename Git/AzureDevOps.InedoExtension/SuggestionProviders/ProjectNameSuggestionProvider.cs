namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class ProjectNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken)
        {
            return this.Client.GetProjectsAsync(cancellationToken);
        }
    }
}
