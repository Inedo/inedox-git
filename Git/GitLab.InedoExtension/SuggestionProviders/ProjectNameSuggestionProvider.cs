using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class ProjectNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];

            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = GitLabCredentials.TryCreate(credentialName, config);
            if (credentials == null)
                return Enumerable.Empty<string>();

            string ownerName = AH.CoalesceString(credentials.GroupName, credentials.UserName);

            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            var client = new GitLabClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.GroupName);
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
