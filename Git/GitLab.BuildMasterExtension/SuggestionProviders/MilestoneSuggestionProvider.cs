using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.BuildMaster.Extensibility;
using Inedo.BuildMaster.Extensibility.Credentials;
using Inedo.BuildMaster.Web.Controls;
using Inedo.Extensions.Clients;
using Inedo.Extensions.Credentials;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : ISuggestionProvider
    {
        public async Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var credentialName = config["CredentialName"];

            if (string.IsNullOrEmpty(credentialName))
                return Enumerable.Empty<string>();

            var credentials = ResourceCredentials.Create<GitLabCredentials>(credentialName);

            string ownerName = AH.CoalesceString(credentials.GroupName, credentials.UserName);
            string repositoryName = AH.CoalesceString(config["ProjectName"], credentials.ProjectName);

            if (string.IsNullOrEmpty(ownerName) || string.IsNullOrEmpty(repositoryName))
                return Enumerable.Empty<string>();

            var client = new GitLabClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.GroupName);

            var milestones = await client.GetMilestonesAsync(repositoryName, "open").ConfigureAwait(false);

            var titles = from m in milestones
                         let title = m["title"]?.ToString()
                         where !string.IsNullOrEmpty(title)
                         select title;

            return new[] { "$ReleaseName", "$ReleaseNumber" }.Concat(titles);
        }
    }
}
