using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class GroupNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var (credentials, resource) = config.GetCredentialsAndResource();
            if (resource == null)
                return Enumerable.Empty<string>();

            var client = new GitLabClient(resource.ApiUrl, credentials?.UserName, credentials?.PersonalAccessToken, resource.GroupName);
            var groups = await client.GetGroupsAsync(CancellationToken.None).ConfigureAwait(false);

            var names = from m in groups
                        let name = m["full_path"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        orderby name
                        select name;

            return names;
        }
    }
}
