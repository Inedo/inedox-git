using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public sealed class MilestoneSuggestionProvider : ISuggestionProvider
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
            string repositoryName = AH.CoalesceString(config["ProjectName"], credentials.ProjectName);

            if (string.IsNullOrEmpty(ownerName) || string.IsNullOrEmpty(repositoryName))
                return Enumerable.Empty<string>();

            var client = new GitLabClient(credentials.ApiUrl, credentials.UserName, credentials.Password, credentials.GroupName);

            var milestones = await client.GetMilestonesAsync(repositoryName, "open", CancellationToken.None).ConfigureAwait(false);

            var titles = from m in milestones
                         let title = m["title"]?.ToString()
                         where !string.IsNullOrEmpty(title)
                         select title;

            if (SDK.ProductName == "BuildMaster")
            {
                titles = new[] { "$ReleaseName", "$ReleaseNumber" }.Concat(titles);
            }

            return titles;
        }
    }
}
