using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Web.Controls;
#endif

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
                client = new GitHubClient(config[nameof(GitHubCredentials.ApiUrl)], config[nameof(GitHubCredentials.UserName)], config[nameof(GitHubCredentials.Password)].ToString().ToSecureString(), config[nameof(GitHubCredentials.OrganizationName)]);
            }
            catch (InvalidOperationException)
            {
                return Enumerable.Empty<string>();
            }

            var repos = await client.GetRepositoriesAsync().ConfigureAwait(false);

            var names = from m in repos
                        let name = m["name"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }

#if Otter
    // remove this when BuildMaster SDK is updated to v5.7, and replace all SecureString extension methods with their AH equivalents
    internal static class SecureStringExtensions
    {
        public static string ToUnsecureString(this SecureString thisValue) => AH.Unprotect(thisValue);
        public static SecureString ToSecureString(this string s) => AH.CreateSecureString(s);
    }
#endif
}
