using System.Security;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;
using Inedo.Extensions.Git.Credentials;

namespace Inedo.Extensions.Git.Legacy
{
    internal interface ILegacyGeneralGitConfiguration : ILegacyGitOperation
    {
        string RepositoryUrl { get; }
        SecureString Password { get; }
        string UserName { get; }

        public static (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ILegacyGeneralGitConfiguration o, ICredentialResolutionContext context)
        {
            GitSecureResourceBase gitResource;

            if (!string.IsNullOrWhiteSpace(o.ResourceName))
            {
                if (!context.TryGetSecureResource(o.ResourceName, out var r) || r is not GitSecureResourceBase gr)
                    throw new ExecutionFailureException("Invalid or missing git resource.");

                gitResource = gr;
            }
            else
            {
                gitResource = new GitSecureResource { RepositoryUrl = o.RepositoryUrl };
            }

            return GetCredentials(gitResource, context, o.UserName, o.Password);
        }
    }
}
