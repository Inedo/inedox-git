using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class CredentialsRepositoryNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string ownerName = AH.CoalesceString(config[nameof(GitHubCredentials.OrganizationName)], config[nameof(GitHubCredentials.UserName)]);

            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            GitHubClient client;
            try
            {
                client = new GitHubClient(config[nameof(GitHubCredentials.ApiUrl)], config[nameof(GitHubCredentials.UserName)], AH.CreateSecureString(config[nameof(GitHubCredentials.Password)].ToString()), config[nameof(GitHubCredentials.OrganizationName)]);
            }
            catch (InvalidOperationException)
            {
                return Enumerable.Empty<string>();
            }

            var repos = await client.GetRepositoriesAsync(CancellationToken.None).ConfigureAwait(false);

            var names = from m in repos
                        let name = m["name"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }
}
