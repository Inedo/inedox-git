using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Clients;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    public abstract class GitLabSuggestionProvider : ISuggestionProvider
    {
        public GitLabSecureCredentials Credentials { get; private set; }
        public GitLabSecureResource Resource { get; private set; }
        public IComponentConfiguration ComponentConfiguration { get; private set; }
        internal GitLabClient Client { get; private set; }

        internal abstract Task<IEnumerable<string>> GetSuggestionsAsync();

        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var context = new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null);

            // resource editors
            var credentialName = config[nameof(GitLabSecureResource.CredentialName)];
            if (!string.IsNullOrEmpty(credentialName))
                this.Credentials = SecureCredentials.TryCreate(credentialName, context) as GitLabSecureCredentials;

            var resourceName = config[nameof(IGitLabConfiguration.ResourceName)];
            if (!string.IsNullOrEmpty(resourceName))
                this.Resource = SecureResource.TryCreate(resourceName, context) as GitLabSecureResource;

            if (this.Credentials == null && this.Resource != null)
                this.Credentials = this.Resource.GetCredentials(context) as GitLabSecureCredentials;

            var groupName = AH.CoalesceString(config[nameof(IGitLabConfiguration.GroupName)], this.Resource?.GroupName);

            if (groupName == null && this.Credentials == null)
                return Task.FromResult(Enumerable.Empty<string>());

            this.ComponentConfiguration = config;

            this.Client = new GitLabClient(
                AH.CoalesceString(config[nameof(IGitLabConfiguration.ApiUrl)], this.Resource?.ApiUrl), 
                AH.CoalesceString(config[nameof(IGitLabConfiguration.UserName)], this.Credentials?.UserName),
                string.IsNullOrEmpty(config[nameof(IGitLabConfiguration.Password)]) 
                    ? this.Credentials?.PersonalAccessToken 
                    : AH.CreateSecureString(config[nameof(IGitLabConfiguration.Password)]), 
                groupName);

            return this.GetSuggestionsAsync();
        }
    }
}
