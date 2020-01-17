using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitHub.Credentials;

namespace Inedo.Extensions.Git
{
    interface IGitConfiguration
    {
        string ResourceName { get; }
        string CredentialName { get; }
        string RepositoryUrl { get; }
        SecureString Password { get; }
        string UserName { get; }
    }

    internal static class GitOperationExtensions
    {
        public static (Extensions.Credentials.UsernamePasswordCredentials, GitSecureResource) GetCredentialsAndResource(this IGitConfiguration operation, ICredentialResolutionContext context)
        {
            // ProjectName could be set directly (via OtterScript) or indirectly (via legacy ResourceCredential)
            if (string.IsNullOrEmpty(operation.RepositoryUrl))
            {
                // for backwards-compatibility, treat the LegacyResourceCredentialName as a ResourceName
                var resourcename = AH.CoalesceString(operation.CredentialName, operation.ResourceName);
                var resource = SecureResource.TryCreate(resourcename, context) as GitSecureResource;
                return ((Extensions.Credentials.UsernamePasswordCredentials)resource.GetCredentials(context), resource);
            }
            else
            {
                return (
                    new Extensions.Credentials.UsernamePasswordCredentials
                    {
                        UserName = operation.UserName,
                        Password = operation.Password
                    },
                    new GitSecureResource
                    {
                        RepositoryUrl = operation.RepositoryUrl
                    });
            }
        }
    }
}
