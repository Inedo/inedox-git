using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;

#if BuildMaster
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
#elif Otter
using Inedo.Otter.Extensibility;
using Inedo.Otter.Extensibility.Credentials;
using Inedo.Otter.Web.Controls;
#endif

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
            var repos = await client.GetProjectsAsync().ConfigureAwait(false);


            var names = from m in repos
                        let name = m["path_with_namespace"]?.ToString()
                        where !string.IsNullOrEmpty(name)
                        select name;

            return new[] { $"{ownerName}/$ApplicationName" }.Concat(names);
        }
    }
}
