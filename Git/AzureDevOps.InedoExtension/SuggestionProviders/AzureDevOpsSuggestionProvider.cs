using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inedo.Extensibility;
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
            (this.Credentials, this.Resource) = config.GetCredentialsAndResource();
            if (this.Resource == null)
                return Task.FromResult(Enumerable.Empty<string>());
            this.Client = new RestApi(this.Credentials?.Token, this.Resource.InstanceUrl, null);
            return this.GetSuggestionsAsync();
        }
    }
}
