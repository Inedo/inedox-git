using System.Collections.Generic;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensions.AzureDevOps.Credentials;
using Inedo.Serialization;
using Inedo.Web;

namespace Inedo.Extensions.AzureDevOps.SuggestionProviders
{
    internal sealed class AzureDevOpsSecureResourceSuggestionProvider : ISuggestionProvider
    {
        Task<IEnumerable<string>> ISuggestionProvider.GetSuggestionsAsync(IComponentConfiguration config)
        {
            IEnumerable<string> GetSuggestions()
            {
                foreach (var resource in SDK.GetSecureResources())
                {
                    var ret = false;
                    try
                    {
                        ret = Persistence.DeserializeFromPersistedObjectXml(resource.Configuration) is AzureDevOpsSecureResource;
                    }
                    catch
                    {
                        continue;
                    }
                    if (ret)
                        yield return resource.Name;
                }
            }
            return Task.FromResult(GetSuggestions());
        }
    }
}
