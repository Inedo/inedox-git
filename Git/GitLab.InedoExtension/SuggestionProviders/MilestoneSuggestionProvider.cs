using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : GitLabSuggestionProvider
    {
        internal override async Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IGitLabConfiguration.ProjectName)], this.Resource?.ProjectName);
            if (string.IsNullOrEmpty(projectName))
                return Enumerable.Empty<string>();

            var milestones = await this.Client.GetMilestonesAsync(projectName, "open", CancellationToken.None).ConfigureAwait(false);
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
