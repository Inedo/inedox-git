using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class CredentialsGroupNameSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            string ownerName = AH.CoalesceString(config[nameof(GitLabCredentials.GroupName)], config[nameof(GitLabCredentials.UserName)]);

            if (string.IsNullOrEmpty(ownerName))
                return Enumerable.Empty<string>();

            GitLabClient client;
            try
            {
                client = new GitLabClient(config[nameof(GitLabCredentials.ApiUrl)], config[nameof(GitLabCredentials.UserName)], AH.CreateSecureString(config[nameof(GitLabCredentials.Password)].ToString()), config[nameof(GitLabCredentials.GroupName)]);
            }
            catch (InvalidOperationException)
            {
                return Enumerable.Empty<string>();
            }

            var groups = await client.GetGroupsAsync(CancellationToken.None).ConfigureAwait(false);

            var names = from m in groups
                        let name = m["full_path"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return names;
        }
    }
}
