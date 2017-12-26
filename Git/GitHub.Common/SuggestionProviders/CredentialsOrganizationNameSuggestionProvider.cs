using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Web.Controls;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Web;
#endif

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class CredentialsOrganizationNameSuggestionProvider : ISuggestionProvider
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

            var orgs = await client.GetOrganizationsAsync(CancellationToken.None).ConfigureAwait(false);

            var names = from m in orgs
                        let name = m["login"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }
}
