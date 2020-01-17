using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Credentials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Inedo.Extensions.GitLab.Operations
{
    internal interface IGitLabConfiguration
    {
        string ResourceName { get;  }
        string CredentialName { get; }
        string GroupName { get;  }
        string ProjectName { get; }
        string ApiUrl { get;  }
        SecureString Password { get; }
        string UserName { get; }
    }

    internal static class GitLabOperationExtensions 
    {
        public static (GitLabSecureCredentials, GitLabSecureResource) GetCredentialsAndResource(this IGitLabConfiguration operation, ICredentialResolutionContext context)
        {
            // ProjectName could be set directly (via OtterScript) or indirectly (via legacy ResourceCredential)
            if (string.IsNullOrEmpty(operation.ProjectName))
            {
                // for backwards-compatibility, treat the LegacyResourceCredentialName as a ResourceName
                var resourcename = AH.CoalesceString(operation.CredentialName, operation.ResourceName);
                var resource = SecureResource.TryCreate(resourcename, context) as GitLabSecureResource;
                return ((GitLabSecureCredentials)resource.GetCredentials(context), resource);
            }
            else
            {
                return (
                    new GitLabSecureCredentials
                    {
                        UserName = operation.UserName,
                        PersonalAccessToken = operation.Password
                    }, 
                    new GitLabSecureResource
                    {
                        ApiUrl = operation.ApiUrl,
                        GroupName = operation.GroupName,
                        ProjectName = operation.ProjectName
                    });
            }
        }
    }
}
