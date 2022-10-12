using System.Runtime.CompilerServices;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class BuildNumberSuggestionProvider : AzureDevOpsSuggestionProvider
    {
        protected override async IAsyncEnumerable<string> GetSuggestionsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IAzureDevOpsConfiguration.ProjectName)], this.Resource?.ProjectName);
            var definitionName = this.ComponentConfiguration["BuildDefinition"];
            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(definitionName))
                yield break;

            await foreach (var b in this.Client.GetBuildsAsync(projectName, definitionName, cancellationToken).ConfigureAwait(false))
                yield return b;
        }
    }
}
