using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public sealed class OrganizationNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config[nameof(IHasCredentials.CredentialName)];

            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<GitHubCredentials>(credentialName);

            string ownerName = AH.CoalesceString(credentials.OrganizationName, credentials.UserName);

            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            var client = new GitHubClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.OrganizationName);

            var orgs = await client.GetOrganizationsAsync(CancellationToken.None).ConfigureAwait(false);

            var names = from m in orgs
                        let name = m["login"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }
}
