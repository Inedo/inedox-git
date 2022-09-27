using System.Security;
using Inedo.Documentation;
using Inedo.ExecutionEngine.Executer;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    [Obsolete]
    internal interface ILegacyGeneralGitConfiguration : ILegacyGitOperation
    {
        string RepositoryUrl { get; }
        SecureString Password { get; }
        string UserName { get; }

        public static (UsernamePasswordCredentials, GitRepository) GetCredentialsAndResource(ILegacyGeneralGitConfiguration o, ICredentialResolutionContext context)
        {
            GitRepository gitResource;

            if (!string.IsNullOrWhiteSpace(o.ResourceName))
            {
                if (!context.TryGetSecureResource(o.ResourceName, out var r) || r is not GitRepository gr)
                    throw new ExecutionFailureException("Invalid or missing git resource.");

                gitResource = gr;
            }
            else
            {
                gitResource = new LegacyGitSecureResourceShim { RepositoryUrl = o.RepositoryUrl };
            }

            return GetCredentials(gitResource, context, o.UserName, o.Password);
        }
    }

    [Obsolete]
    internal sealed class LegacyGitSecureResourceShim : GitSecureResourceBase<Extensions.Credentials.UsernamePasswordCredentials, GitSecureCredentialsBase>
    {
        public string RepositoryUrl { get; set; }
        public override RichDescription GetDescription() => new RichDescription(this.RepositoryUrl);
        public override Task<string> GetRepositoryUrlAsync(ICredentialResolutionContext context, CancellationToken cancellationToken) => Task.FromResult(this.RepositoryUrl);
    }
}
