using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : GitHubSuggestionProvider
    {
        internal async override Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            string repositoryName = AH.CoalesceString(this.ComponentConfiguration[nameof(IGitHubConfiguration.RepositoryName)], this.Resource?.RepositoryName);
            string ownerName = AH.CoalesceString(
                this.ComponentConfiguration[nameof(IGitHubConfiguration.OrganizationName)], this.Resource?.OrganizationName,
                this.ComponentConfiguration[nameof(IGitHubConfiguration.UserName)], this.Credentials?.UserName
                );
            if (string.IsNullOrEmpty(ownerName) || string.IsNullOrEmpty(repositoryName))
                return Enumerable.Empty<string>();

            var milestones = await this.Client.GetMilestonesAsync(ownerName, repositoryName, "open", CancellationToken.None).ConfigureAwait(false);

            var titles = from m in milestones
                         let title = m["title"]?.ToString()
                         where !string.IsNullOrEmpty(title)
                         select title;

            if (SDK.ProductName == "BuildMaster")
            {
                titles = new[] { "$ReleaseName", "$ReleaseNumber" }.Concat(titles);
            }
            return titles;
        }
    }
}
