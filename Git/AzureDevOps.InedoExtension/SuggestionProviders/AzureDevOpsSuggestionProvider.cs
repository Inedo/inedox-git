using System.Runtime.CompilerServices;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal abstract class AzureDevOpsSuggestionProvider : ISuggestionProvider
    {
        protected AzureDevOpsSuggestionProvider()
        {
        }

        protected IComponentConfiguration ComponentConfiguration { get; private set; }
        protected AzureDevOpsAccount Credentials { get; private set; }
        protected AzureDevOpsRepository Resource { get; private set; }
        protected AzureDevOpsClient Client { get; private set; }

        protected abstract IAsyncEnumerable<string> GetSuggestionsAsync(CancellationToken cancellationToken);

        private async IAsyncEnumerable<string> GetSuggestionsInternalAsync(string startsWith, IComponentConfiguration config, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                var context = new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null);
                var credentialName = config[nameof(AzureDevOpsRepository.CredentialName)];
                if (!string.IsNullOrEmpty(credentialName))
                    this.Credentials = SecureCredentials.TryCreate(credentialName, context) as AzureDevOpsAccount;

                var resourceName = config[nameof(IAzureDevOpsConfiguration.ResourceName)];
                if (!string.IsNullOrEmpty(resourceName))
                    this.Resource = SecureResource.TryCreate(resourceName, context) as AzureDevOpsRepository;

                if (this.Credentials == null && this.Resource != null)
                    this.Credentials = this.Resource.GetCredentials(context) as AzureDevOpsAccount;

                var instanceUrl = AH.CoalesceString(config[nameof(IAzureDevOpsConfiguration.InstanceUrl)], this.Credentials?.ServiceUrl, this.Resource?.LegacyInstanceUrl);

                if (instanceUrl == null && this.Credentials == null)
                    yield break;

                this.ComponentConfiguration = config;

                var token = string.IsNullOrEmpty(config[nameof(IAzureDevOpsConfiguration.Token)]) ? AH.Unprotect(this.Credentials.Password) : config[nameof(IAzureDevOpsConfiguration.Token)];
                if (string.IsNullOrEmpty(token))
                    yield break;

                this.Client = new AzureDevOpsClient(instanceUrl, token);

                await foreach (var s in this.GetSuggestionsAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (string.IsNullOrEmpty(startsWith) || s.StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                        yield return s;
                }
            }
            finally
            {
                this.Client?.Dispose();
            }
        }

        IAsyncEnumerable<string> ISuggestionProvider.GetSuggestionsAsync(string startsWith, IComponentConfiguration config, CancellationToken cancellationToken)
        {
            return this.GetSuggestionsInternalAsync(startsWith, config, cancellationToken);
        }
        async Task<IEnumerable<string>> ISuggestionProvider.GetSuggestionsAsync(IComponentConfiguration config)
        {
            var list = new List<string>();
            await foreach (var s in this.GetSuggestionsInternalAsync(string.Empty, config, default).ConfigureAwait(false))
                list.Add(s);
            return list;
        }
    }
}
