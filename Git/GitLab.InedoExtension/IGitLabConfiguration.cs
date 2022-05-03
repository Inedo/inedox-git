using System.Security;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Credentials;

namespace Inedo.Extensions.GitLab
{
    internal interface IGitLabConfiguration
    {
        string ResourceName { get;  }
        string GroupName { get;  }
        string ProjectName { get; }
        string ApiUrl { get;  }
        SecureString Password { get; }
        string UserName { get; }
    }
    internal static class GitLabOperationExtensions 
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IGitLabConfiguration.ProjectName)],
                config[nameof(IGitLabConfiguration.ResourceName)],
                "(unknown)");
        }

        public static (GitLabSecureCredentials, GitLabSecureResource) GetCredentialsAndResource(this IGitLabConfiguration operation, ICredentialResolutionContext context)
        {
            GitLabSecureCredentials credentials; GitLabSecureResource resource; 
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                credentials = string.IsNullOrEmpty(operation.UserName) ? null : new GitLabSecureCredentials();
                resource = string.IsNullOrEmpty(AH.CoalesceString(operation.ProjectName, operation.GroupName, operation.ApiUrl)) ? null : new GitLabSecureResource();
            }
            else
            {
                resource = (GitLabSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    credentials = null;
                }
                else
                {
                    credentials = (GitLabSecureCredentials)resource.GetCredentials(context);
                }
            }

            if (credentials != null)
            {
                credentials.UserName = AH.CoalesceString(operation.UserName, credentials.UserName);
                credentials.PersonalAccessToken = operation.Password ?? credentials.PersonalAccessToken;
            }
            if (resource != null)
            {
                resource.ApiUrl = AH.CoalesceString(operation.ApiUrl, resource.ApiUrl);
                resource.GroupName = AH.CoalesceString(operation.GroupName, resource.GroupName);
                resource.ProjectName = AH.CoalesceString(operation.ProjectName, resource.ProjectName);
            }

            return (credentials, resource);
        }
    }
}
