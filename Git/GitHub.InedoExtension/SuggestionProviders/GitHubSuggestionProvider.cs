using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Clients;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitHub.SuggestionProviders
{
    public abstract class GitHubSuggestionProvider : ISuggestionProvider
    {
        public GitHubSecureCredentials Credentials { get; private set; }
        public GitHubSecureResource Resource { get; private set; }
        public IComponentConfiguration ComponentConfiguration { get; private set; }
        internal GitHubClient Client { get; private set; }

        internal abstract Task<IEnumerable<string>> GetSuggestionsAsync();

        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var context = new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null);

            // resource editors
            var credentialName = config[nameof(GitHubSecureResource.CredentialName)];
            if (!string.IsNullOrEmpty(credentialName))
                this.Credentials = SecureCredentials.TryCreate(credentialName, context) as GitHubSecureCredentials;

            var resourceName = config[nameof(IGitHubConfiguration.ResourceName)];
            if (!string.IsNullOrEmpty(resourceName))
                this.Resource = SecureResource.TryCreate(resourceName, context) as GitHubSecureResource;

            if (this.Credentials == null && this.Resource != null)
                this.Credentials = this.Resource.GetCredentials(context) as GitHubSecureCredentials;

            var groupName = AH.CoalesceString(config[nameof(IGitHubConfiguration.OrganizationName)], this.Resource?.OrganizationName);

            if (groupName == null && this.Credentials == null)
                return Task.FromResult(Enumerable.Empty<string>());

            this.ComponentConfiguration = config;

            this.Client = new GitHubClient(
                AH.CoalesceString(config[nameof(IGitHubConfiguration.ApiUrl)], this.Resource?.ApiUrl), 
                AH.CoalesceString(config[nameof(IGitHubConfiguration.UserName)], this.Credentials?.UserName),
                string.IsNullOrEmpty(config[nameof(IGitHubConfiguration.Password)]) 
                    ? this.Credentials?.Password 
                    : AH.CreateSecureString(config[nameof(IGitHubConfiguration.Password)]), 
                groupName);

            return this.GetSuggestionsAsync();
        }
    }
}
