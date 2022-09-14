using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    internal interface ILegacyGitHubOperation : ILegacyGitOperation
    {
        string OrganizationName { get; }
        string RepositoryName { get; }
        string ApiUrl { get; }
        SecureString Password { get; }
        string UserName { get; }

        public static (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ILegacyGitHubOperation o, ICredentialResolutionContext context)
        {
            var gitResource = o.GetResource(context, "Inedo.Extensions.GitHub.Credentials.GitHubSecureResource", "GitHub");

            dynamic gitHubResource = gitResource;
            if (!string.IsNullOrEmpty(o.OrganizationName))
                gitHubResource.OrganizationName = o.OrganizationName;
            if (!string.IsNullOrEmpty(o.RepositoryName))
                gitHubResource.RepositoryName = o.RepositoryName;
            if (!string.IsNullOrEmpty(o.ApiUrl))
                gitHubResource.ApiUrl = o.ApiUrl;

            return (((GitSecureCredentialsBase)gitResource.GetCredentials(context))?.ToUsernamePassword(), gitResource);
        }
    }
}
