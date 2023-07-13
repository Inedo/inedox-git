using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    [Obsolete]
    internal interface ILegacyGitHubOperation : ILegacyGitOperation
    {
        string OrganizationName { get; }
        string RepositoryName { get; }
        string ApiUrl { get; }
        SecureString Password { get; }
        string UserName { get; }

        public static (UsernamePasswordCredentials, GitRepository) GetCredentialsAndResource(ILegacyGitHubOperation o, ICredentialResolutionContext context)
        {
            var gitResource = o.GetResource(context, "Inedo.Extensions.GitHub.GitHubRepository", "GitHub");

            dynamic gitHubResource = gitResource;
            if (!string.IsNullOrEmpty(o.OrganizationName))
                gitHubResource.OrganizationName = o.OrganizationName;
            if (!string.IsNullOrEmpty(o.RepositoryName))
                gitHubResource.RepositoryName = o.RepositoryName;
            if (!string.IsNullOrEmpty(o.ApiUrl))
                gitHubResource.LegacyApiUrl = o.ApiUrl;

            return (((GitServiceCredentials)gitResource.GetCredentials(context))?.ToUsernamePassword(), gitResource);
        }
    }
}
