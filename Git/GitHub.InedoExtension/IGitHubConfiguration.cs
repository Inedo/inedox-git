using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Credentials;

namespace Inedo.Extensions.GitHub
{
    internal interface IGitHubConfiguration
    {
        string ResourceName { get; }
        string OrganizationName { get; }
        string RepositoryName { get; }
        string ApiUrl { get; }
        SecureString Password { get; }
        string UserName { get; }
    }

    internal static class GitHubOperationExtensions
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IGitHubConfiguration.RepositoryName)], 
                config[nameof(IGitHubConfiguration.ResourceName)],
                "(unknown)");
        }
        public static (GitHubSecureCredentials, GitHubSecureResource) GetCredentialsAndResource(this IGitHubConfiguration operation, ICredentialResolutionContext context)
        {
            GitHubSecureCredentials credentials; GitHubSecureResource resource;
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                credentials = string.IsNullOrEmpty(operation.UserName) ? null : new GitHubSecureCredentials();
                resource = string.IsNullOrEmpty(AH.CoalesceString(operation.RepositoryName, operation.OrganizationName, operation.ApiUrl)) ? null : new GitHubSecureResource();
            }
            else
            {
                resource = (GitHubSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    var rc = SecureCredentials.TryCreate(operation.ResourceName, context) as GitHubLegacyResourceCredentials;
                    resource = (GitHubSecureResource)rc?.ToSecureResource();
                    credentials = (GitHubSecureCredentials)rc?.ToSecureCredentials();
                }
                else
                {
                    credentials = (GitHubSecureCredentials)resource.GetCredentials(context);
                }
            }

            if (credentials != null)
            {
                credentials.UserName = AH.CoalesceString(operation.UserName, credentials.UserName);
                credentials.Password = operation.Password ?? credentials.Password;
            }

            if (resource != null)
            {
                resource.ApiUrl = AH.CoalesceString(operation.ApiUrl, resource.ApiUrl);
                resource.OrganizationName = AH.CoalesceString(operation.OrganizationName, resource.OrganizationName);
                resource.RepositoryName = AH.CoalesceString(operation.RepositoryName, resource.RepositoryName);
            }

            return (credentials, resource);
        }
    }
}
