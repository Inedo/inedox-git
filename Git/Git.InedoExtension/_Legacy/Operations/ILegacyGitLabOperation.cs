using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Git;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    [Obsolete]
    internal interface ILegacyGitLabOperation : ILegacyGitOperation
    {
        string GroupName { get; }
        string ProjectName { get; }
        string ApiUrl { get; }
        SecureString Password { get; }
        string UserName { get; }

        public static (UsernamePasswordCredentials, GitRepository) GetCredentialsAndResource(ILegacyGitLabOperation o, ICredentialResolutionContext context)
        {
            var gitResource = o.GetResource(context, "Inedo.Extensions.GitLab.GitLabRepository", "GitLab");

            dynamic gitHubResource = gitResource;
            if (!string.IsNullOrEmpty(o.GroupName))
                gitHubResource.GroupName = o.GroupName;
            if (!string.IsNullOrEmpty(o.ProjectName))
                gitHubResource.ProjectName = o.ProjectName;
            if (!string.IsNullOrEmpty(o.ApiUrl))
                gitHubResource.LegacyApiUrl = o.ApiUrl;

            return (((GitServiceCredentials)gitResource.GetCredentials(context))?.ToUsernamePassword(), gitResource);
        }
    }
}
