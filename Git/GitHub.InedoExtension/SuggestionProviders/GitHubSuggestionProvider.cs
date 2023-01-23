using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public abstract class GitHubSuggestionProvider : ISuggestionProvider
    {
        public GitHubAccount Credentials { get; private set; }
        public GitHubRepository Resource { get; private set; }
        public IComponentConfiguration ComponentConfiguration { get; private set; }
        internal GitHubClient Client { get; private set; }

        internal abstract Task<IEnumerable<string>> GetSuggestionsAsync();

        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var context = new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null);

            // resource editors
            var credentialName = config[nameof(GitHubRepository.CredentialName)];
            if (!string.IsNullOrEmpty(credentialName))
                this.Credentials = SecureCredentials.TryCreate(credentialName, context) as GitHubAccount;

            var resourceName = config[nameof(IGitHubConfiguration.ResourceName)];
            if (!string.IsNullOrEmpty(resourceName))
                this.Resource = SecureResource.TryCreate(resourceName, context) as GitHubRepository;

            if (this.Credentials == null && this.Resource != null)
                this.Credentials = this.Resource.GetCredentials(context) as GitHubAccount;

            var groupName = AH.CoalesceString(config[nameof(IGitHubConfiguration.OrganizationName)], this.Resource?.OrganizationName);

            if (groupName == null && this.Credentials == null)
                return Task.FromResult(Enumerable.Empty<string>());

            this.ComponentConfiguration = config;

            this.Client = new GitHubClient(
                AH.CoalesceString(config[nameof(IGitHubConfiguration.ApiUrl)], this.Credentials?.ServiceUrl, this.Resource?.LegacyApiUrl), 
                AH.CoalesceString(config[nameof(IGitHubConfiguration.UserName)], this.Credentials?.UserName),
                string.IsNullOrEmpty(config[nameof(IGitHubConfiguration.Password)]) 
                    ? this.Credentials?.Password 
                    : AH.CreateSecureString(config[nameof(IGitHubConfiguration.Password)])
                );

            return this.GetSuggestionsAsync();
        }

        private protected static async Task<IEnumerable<T>> MakeAsync<T>(IAsyncEnumerable<T> value)
        {
            var list = new List<T>();
            await foreach (var v in value.ConfigureAwait(false))
                list.Add(v);
            return list;
        }
    }
}
