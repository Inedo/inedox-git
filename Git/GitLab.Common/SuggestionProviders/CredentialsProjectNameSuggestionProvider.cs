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

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class CredentialsProjectNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string ownerName = AH.CoalesceString(config[nameof(GitLabCredentials.GroupName)], config[nameof(GitLabCredentials.UserName)]);

            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            GitLabClient client;
            try
            {
                client = new GitLabClient(config[nameof(GitLabCredentials.ApiUrl)], config[nameof(GitLabCredentials.UserName)], config[nameof(GitLabCredentials.Password)].ToString().ToSecureString(), config[nameof(GitLabCredentials.GroupName)]);
            }
            catch (InvalidOperationException)
            {
                return Enumerable.Empty<string>();
            }

            var repos = await client.GetProjectsAsync().ConfigureAwait(false);

            var names = from m in repos
                        let name = m["path_with_namespace"]?.ToString()
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
