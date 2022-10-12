namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class BuildDefinitionNameSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken)
        {
            return this.Client.GetBuildDefinitionsAsync(this.Resource.ProjectName, cancellationToken);
        }
    }
}
