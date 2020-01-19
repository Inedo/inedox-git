using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var (credentials, resource) = config.GetCredentialsAndResource();
            if (resource == null)
                return Enumerable.Empty<string>();
            var client = new GitHubClient(resource.ApiUrl, credentials?.UserName, credentials?.Password, resource.OrganizationName);

            string ownerName = AH.CoalesceString(resource.OrganizationName, credentials?.UserName);
            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            var milestones = await client.GetMilestonesAsync(ownerName, resource.RepositoryName, "open", CancellationToken.None).ConfigureAwait(false);

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
