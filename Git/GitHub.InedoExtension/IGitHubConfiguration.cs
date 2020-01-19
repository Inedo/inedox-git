using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Credentials;
using Inedo.Web.Handlers;
using Inedo.Web.Plans;

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
    internal sealed class GitHubConfiguration : IGitHubConfiguration
    {
        public string ResourceName { get; set; }
        public string OrganizationName { get; set; }
        public string RepositoryName { get; set; }
        public string ApiUrl { get; set; }
        public SecureString Password { get; set; }
        public string UserName { get; set; }
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

        public static (GitHubSecureCredentials, GitHubSecureResource) GetCredentialsAndResource(this IComponentConfiguration config)
        {
            return GetCredentialsAndResource(
                new GitHubConfiguration 
                { 
                    ApiUrl = AH.NullIf(config[nameof(IGitHubConfiguration.ApiUrl)], string.Empty),
                    OrganizationName = AH.NullIf(config[nameof(IGitHubConfiguration.OrganizationName)], string.Empty),
                    Password = string.IsNullOrEmpty(config[nameof(IGitHubConfiguration.Password)]) ? null : AH.CreateSecureString(config[nameof(IGitHubConfiguration.Password)]),
                    RepositoryName = AH.NullIf(config[nameof(IGitHubConfiguration.RepositoryName)], string.Empty),
                    ResourceName = AH.NullIf(config[nameof(IGitHubConfiguration.ResourceName)], string.Empty),
                    UserName = AH.NullIf(config[nameof(IGitHubConfiguration.UserName)], string.Empty)
                }, 
                new CredentialResolutionContext((config.EditorContext as IOperationEditorContext)?.ProjectId, null));
        }
        public static (GitHubSecureCredentials, GitHubSecureResource) GetCredentialsAndResource(this IGitHubConfiguration operation, ICredentialResolutionContext context)
        {
            GitHubSecureCredentials credentials = null; 
            GitHubSecureResource resource = null;
            if (!string.IsNullOrEmpty(operation.ResourceName))
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

            return (
                new GitHubSecureCredentials
                {
                    UserName = AH.CoalesceString(operation.UserName, credentials?.UserName),
                    Password = operation.Password ?? credentials?.Password
                },
                new GitHubSecureResource
                {
                    ApiUrl = AH.CoalesceString(operation.ApiUrl, resource?.ApiUrl),
                    OrganizationName = AH.CoalesceString(operation.OrganizationName, resource?.OrganizationName),
                    RepositoryName = AH.CoalesceString(operation.RepositoryName, resource?.RepositoryName)
                }
            );

        }
    }
}
