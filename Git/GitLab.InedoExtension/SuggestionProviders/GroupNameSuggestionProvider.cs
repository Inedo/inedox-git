using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class GroupNameSuggestionProvider : GitLabSuggestionProvider
    {
        internal override async Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            if (this.Credentials == null)
                return Enumerable.Empty<string>();

            var groups = await this.Client.GetGroupsAsync(CancellationToken.None).ConfigureAwait(false);
            var names = from m in groups
                        let name = m["full_path"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        orderby name
                        select name;

            return names;
        }
    }
}
