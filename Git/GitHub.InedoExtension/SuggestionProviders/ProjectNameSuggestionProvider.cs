using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensions.GitHub.IssueSources;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    internal sealed class ProjectNameSuggestionProvider : GitHubSuggestionProvider
    {
        internal override async Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var repositoryName = AH.NullIf(this.ComponentConfiguration[nameof(GitHubProjectIssueSource.RepositoryName)], string.Empty);
            var projects = await this.Client.GetProjectsAsync(this.Resource.OrganizationName, repositoryName, CancellationToken.None);
            return from p in projects
                   let name = p["name"]?.ToString()
                   where !string.IsNullOrEmpty(name)
                   select name;
        }
    }
}