using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Credentials;

namespace Inedo.Extensions.GitHub
{
    interface IGitHubConfiguration
    {
        string ResourceName { get; }
        string CredentialName { get; }
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
            //var credentials = string.IsNullOrEmpty(config[nameof(CredentialName)]) ? null : GitHubCredentials.TryCreate(config[nameof(CredentialName)], config);
            //var repositoryOwner = AH.CoalesceString(config[nameof(OrganizationName)], credentials?.OrganizationName, config[nameof(UserName)], credentials?.UserName, "(unknown)");
            //var repositoryName = AH.CoalesceString(config[nameof(RepositoryName)], credentials?.RepositoryName, "(unknown)");
            return AH.CoalesceString(config[nameof(IGitHubConfiguration.RepositoryName)], config[nameof(IGitHubConfiguration.CredentialName)], config[nameof(IGitHubConfiguration.ResourceName)]);
        }

        public static (GitHubSecureCredentials, GitHubSecureResource) GetCredentialsAndResource(this IGitHubConfiguration operation, ICredentialResolutionContext context)
        {
            // ProjectName could be set directly (via OtterScript) or indirectly (via legacy ResourceCredential)
            if (string.IsNullOrEmpty(operation.RepositoryName))
            {
                // for backwards-compatibility, treat the LegacyResourceCredentialName as a ResourceName
                var resourcename = AH.CoalesceString(operation.CredentialName, operation.ResourceName);
                var resource = SecureResource.TryCreate(resourcename, context) as GitHubSecureResource;
                return ((GitHubSecureCredentials)resource.GetCredentials(context), resource);
            }
            else
            {
                return (
                    new GitHubSecureCredentials
                    {
                        UserName = operation.UserName,
                        Password = operation.Password
                    },
                    new GitHubSecureResource
                    {
                        ApiUrl = operation.ApiUrl,
                        OrganizationName = operation.OrganizationName,
                        RepositoryName = operation.RepositoryName
                    });
            }
        }
    }
}
