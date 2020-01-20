using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class ProjectNameSuggestionProvider : GitLabSuggestionProvider
    {
        internal override async Task<IEnumerable<string>> GetSuggestionsAsync()
        {
            var repos = await this.Client.GetProjectsAsync(CancellationToken.None).ConfigureAwait(false);
            var names = from m in repos
                        let name = m["path"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        orderby name
                        select name;

            if (SDK.ProductName == "BuildMaster")
            {
                names = new[] { $"$ApplicationName" }.Concat(names);
            }
            return names;
        }
    }
}
