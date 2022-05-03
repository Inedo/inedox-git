using System.Security;
using System.Threading.Tasks;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
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
    internal static class GitOperationExtensions
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IGitConfiguration.RepositoryUrl)],
                config[nameof(IGitConfiguration.ResourceName)],
                "(unknown)");
        }
        public static async Task<(UsernamePasswordCredentials, GitSecureResourceBase)> GetCredentialsAndResourceAsync(this IGitConfiguration operation, IOperationExecutionContext opcontext)
        {
            var context = (ICredentialResolutionContext)opcontext;
            UsernamePasswordCredentials credentials = null;
            GitSecureResourceBase resource = null;
            if (!string.IsNullOrEmpty(operation.ResourceName))
            {
                resource = (GitSecureResourceBase)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    credentials = null;
                }
                else
                {
                    credentials = (UsernamePasswordCredentials)resource.GetCredentials(context);
                }
            }
            string repositoryUrl = operation.RepositoryUrl;
            if (string.IsNullOrEmpty(repositoryUrl) && resource != null)
            {
                repositoryUrl = await resource.GetRepositoryUrlAsync(context, opcontext.CancellationToken);
            }

            return (
                string.IsNullOrEmpty(AH.CoalesceString(operation.UserName, credentials?.UserName)) ? null : new UsernamePasswordCredentials
                    {
                        UserName = AH.CoalesceString(operation.UserName, credentials?.UserName),
                        Password = operation.Password ?? credentials?.Password
                    },
                new GitSecureResource
                {
                    RepositoryUrl = repositoryUrl
                }
            );

        }
    }
}
