using System;
using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;

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
    internal record GitHubProjectId(string OrganizationName, string RepositoryName)
    {
        public GitHubProjectId(GitHubRepository repository)
            : this(repository.RepositoryName, repository.OrganizationName)
        {
        }

        public static implicit operator GitHubProjectId(GitHubRepository r) => new(r);

        public string ToUriFragment()
        {
            if (!string.IsNullOrEmpty(this.OrganizationName))
                return Uri.EscapeDataString(this.OrganizationName + "/" + this.RepositoryName);
            else
                return Uri.EscapeDataString(this.OrganizationName ?? string.Empty);
        }
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
        public static (GitHubAccount, GitHubRepository) GetCredentialsAndResource(this IGitHubConfiguration operation, ICredentialResolutionContext context)
        {
            GitHubAccount credentials; GitHubRepository resource;
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                credentials = string.IsNullOrEmpty(operation.UserName) ? null : new GitHubAccount();
                resource = string.IsNullOrEmpty(AH.CoalesceString(operation.RepositoryName, operation.OrganizationName, operation.ApiUrl)) ? null : new GitHubRepository();
            }
            else
            {
                resource = (GitHubRepository)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    credentials = null;
                }
                else
                {
                    credentials = (GitHubAccount)resource.GetCredentials(context);
                }
            }

            if (credentials != null)
            {
                credentials.UserName = AH.CoalesceString(operation.UserName, credentials.UserName);
                credentials.Password = operation.Password ?? credentials.Password;
                credentials.ServiceUrl = AH.CoalesceString(operation.ApiUrl, credentials.ServiceUrl, resource?.LegacyApiUrl);
            }

            if (resource != null)
            {
                resource.OrganizationName = AH.CoalesceString(operation.OrganizationName, resource.OrganizationName);
                resource.RepositoryName = AH.CoalesceString(operation.RepositoryName, resource.RepositoryName);
            }

            return (credentials, resource);
        }
    }
}
