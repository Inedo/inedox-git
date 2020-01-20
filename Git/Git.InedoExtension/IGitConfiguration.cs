using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
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
        public static (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(this IGitConfiguration operation, IOperationExecutionContext opcontext)
        {
            var context = (ICredentialResolutionContext)opcontext;
            UsernamePasswordCredentials credentials = null;
            GitSecureResourceBase resource = null;
            if (!string.IsNullOrEmpty(operation.ResourceName))
            {
                resource = (GitSecureResourceBase)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    var rc = SecureCredentials.TryCreate(operation.ResourceName, context) as GeneralGitCredentials;
                    resource = (GitSecureResourceBase)rc?.ToSecureResource();
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
                    RepositoryUrl = AH.CoalesceString(operation.RepositoryUrl, resource?.GetRepositoryUrl(context, opcontext.CancellationToken))
                }
            );

        }
    }
}
