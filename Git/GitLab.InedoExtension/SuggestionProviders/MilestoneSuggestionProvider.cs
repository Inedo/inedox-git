using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : GitLabSuggestionProvider
    {
        private protected override async Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var projectName = AH.CoalesceString(this.ComponentConfiguration[nameof(IGitLabConfiguration.ProjectName)], this.Resource?.ProjectName);
            if (string.IsNullOrEmpty(projectName))
                return Enumerable.Empty<string>();

            var groupName = AH.CoalesceString(this.ComponentConfiguration[nameof(IGitLabConfiguration.GroupName)], this.Resource?.GroupName);

            var milestones = await MakeAsync(this.Client.GetMilestonesAsync(new GitLabProjectId(groupName, projectName), "open", CancellationToken.None)).ConfigureAwait(false);
            return milestones
                .OrderByDescending(m => m.Id)
                .Select(m => m.Title);
        }
    }
}
