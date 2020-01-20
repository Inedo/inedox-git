using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class ProjectNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var (credentials, resource) = config.GetCredentialsAndResource();
            if (resource == null)
                return Enumerable.Empty<string>();

            var client = new GitLabClient(resource.ApiUrl, credentials?.UserName, credentials?.PersonalAccessToken, resource.GroupName);
            var repos = await client.GetProjectsAsync(CancellationToken.None).ConfigureAwait(false);
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
