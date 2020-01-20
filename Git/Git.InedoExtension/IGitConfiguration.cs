using System.Security;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Git.Credentials;
using UsernamePasswordCredentials = Inedo.Extensions.Credentials.UsernamePasswordCredentials;

namespace Inedo.Extensions.Git
{
    interface IGitConfiguration
    {
        string ResourceName { get; }
        string RepositoryUrl { get; }
        SecureString Password { get; }
        string UserName { get; }
    }
    internal sealed class GitConfiguration : IGitConfiguration
    {
        public string ResourceName { get; set; }
        public string RepositoryUrl { get; set; }
        public SecureString Password { get; set; }
        public string UserName { get; set; }
    }

    internal static class GitOperationExtensions
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IGitConfiguration.RepositoryUrl)],
                config[nameof(IGitConfiguration.ResourceName)],
                "(unknown)");
        }

        public static (UsernamePasswordCredentials, GitSecureResource) GetCredentialsAndResource(this IComponentConfiguration config)
        {
            return GetCredentialsAndResource(
                new GitConfiguration
                {
                    RepositoryUrl = AH.NullIf(config[nameof(IGitConfiguration.RepositoryUrl)], string.Empty),
                    Password = string.IsNullOrEmpty(config[nameof(IGitConfiguration.Password)]) ? null : AH.CreateSecureString(config[nameof(IGitConfiguration.Password)]),
                    ResourceName = AH.NullIf(config[nameof(IGitConfiguration.ResourceName)], string.Empty),
                    UserName = AH.NullIf(config[nameof(IGitConfiguration.UserName)], string.Empty)
                },
                new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null));
        }
        public static (UsernamePasswordCredentials, GitSecureResource) GetCredentialsAndResource(this IGitConfiguration operation, IOperationExecutionContext context)
            => GetCredentialsAndResource(operation, (ICredentialResolutionContext)context);
        public static (UsernamePasswordCredentials, GitSecureResource) GetCredentialsAndResource(this IGitConfiguration operation, ICredentialResolutionContext context)
        {
            UsernamePasswordCredentials credentials = null;
            GitSecureResource resource = null;
            if (!string.IsNullOrEmpty(operation.ResourceName))
            {
                resource = (GitSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    var rc = SecureCredentials.TryCreate(operation.ResourceName, context) as GeneralGitCredentials;
                    resource = (GitSecureResource)rc?.ToSecureResource();
                    credentials = (UsernamePasswordCredentials)rc?.ToSecureCredentials();
                }
                else
                {
                    credentials = (UsernamePasswordCredentials)resource.GetCredentials(context);
                }
            }

            return (
                string.IsNullOrEmpty(AH.CoalesceString(operation.UserName, credentials?.UserName)) ? null : new UsernamePasswordCredentials
                    {
                        UserName = AH.CoalesceString(operation.UserName, credentials?.UserName),
                        Password = operation.Password ?? credentials?.Password
                    },
                new GitSecureResource
                {
                    RepositoryUrl = AH.CoalesceString(operation.RepositoryUrl, resource?.RepositoryUrl)
                }
            );

        }
    }
}
