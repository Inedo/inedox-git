using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.AzureDevOps.Clients.Rest;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    public abstract class AzureDevOpsSuggestionProvider : ISuggestionProvider
    {
        protected IComponentConfiguration ComponentConfiguration { get; private set;  }
        protected AzureDevOpsSecureCredentials Credentials { get; private set; }
        protected AzureDevOpsSecureResource Resource { get; private set; }
        internal RestApi Client { get; private set; }

        internal abstract Task<IEnumerable<string>> GetSuggestionsAsync();
        public Task<IEnumerable<string>> GetSuggestionsAsync(IComponentConfiguration config)
        {
            var context = new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null);

            // resource editors
            var credentialName = config[nameof(AzureDevOpsSecureResource.CredentialName)];
            if (!string.IsNullOrEmpty(credentialName))
                this.Credentials = SecureCredentials.TryCreate(credentialName, context) as AzureDevOpsSecureCredentials;

            var resourceName = config[nameof(IAzureDevOpsConfiguration.ResourceName)];
            if (!string.IsNullOrEmpty(resourceName))
                this.Resource = SecureResource.TryCreate(resourceName, context) as AzureDevOpsSecureResource;

            if (this.Credentials == null && this.Resource != null)
                this.Credentials = this.Resource.GetCredentials(context) as AzureDevOpsSecureCredentials;

            var instanceUrl = AH.CoalesceString(config[nameof(IAzureDevOpsConfiguration.InstanceUrl)], this.Resource?.InstanceUrl);

            if (instanceUrl == null && this.Credentials == null)
                return Task.FromResult(Enumerable.Empty<string>());

            this.ComponentConfiguration = config;

            this.Client = new RestApi(
                string.IsNullOrEmpty(config[nameof(IAzureDevOpsConfiguration.Token)])
                    ? this.Credentials?.Token
                    : AH.CreateSecureString(config[nameof(IAzureDevOpsConfiguration.Token)]),
                instanceUrl,
                null);
            return this.GetSuggestionsAsync();
        }
    }
}
