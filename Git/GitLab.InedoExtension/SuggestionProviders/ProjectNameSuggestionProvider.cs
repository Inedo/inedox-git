using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
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

            var credentials = ResourceCredentials.Create<GitLabCredentials>(credentialName);

            string ownerName = AH.CoalesceString(credentials.GroupName, credentials.UserName);

            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            var client = new GitLabClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.GroupName);
            var repos = await client.GetProjectsAsync(CancellationToken.None).ConfigureAwait(false);


            var names = from m in repos
                        let name = m["path_with_namespace"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            if (SDK.ProductName == "BuildMaster")
            {
                names = new[] { $"{ownerName}/$ApplicationName" }.Concat(names);
            }
            return names;
        }
    }
}
