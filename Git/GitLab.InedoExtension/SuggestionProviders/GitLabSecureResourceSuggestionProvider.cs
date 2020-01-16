using Inedo.Extensibility;
using Inedo.Extensions.GitLab.Credentials;
using Inedo.Serialization;
using Inedo.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.SuggestionProviders
{
    internal sealed class GitLabSecureResourceSuggestionProvider : ISuggestionProvider
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
                        ret = Persistence.DeserializeFromPersistedObjectXml(resource.Configuration) is GitLabSecureResource;
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
