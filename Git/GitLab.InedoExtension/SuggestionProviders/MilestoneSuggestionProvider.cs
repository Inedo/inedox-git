using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var (credentials, resource) = config.GetCredentialsAndResource();
            if (resource == null)
                return Enumerable.Empty<string>();

            var client = new GitLabClient(resource.ApiUrl, credentials?.UserName, credentials?.PersonalAccessToken, resource.GroupName);
            var milestones = await client.GetMilestonesAsync(resource.ProjectName, "open", CancellationToken.None).ConfigureAwait(false);
            var titles = from m in milestones
                         let title = m["title"]?.ToString()
                         let iid = m.Value<int?>("iid")
                         where !string.IsNullOrEmpty(title) && iid.HasValue
                         orderby iid descending
                         select title;

            if (SDK.ProductName == "BuildMaster")
            {
                titles = new[] { "$ReleaseName", "$ReleaseNumber" }.Concat(titles);
            }

            return titles;
        }
    }
}
