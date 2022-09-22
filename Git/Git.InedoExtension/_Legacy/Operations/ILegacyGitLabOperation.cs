using System.Security;
using Inedo.Extensibility.Credentials;
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

        public static (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ILegacyGitLabOperation o, ICredentialResolutionContext context)
        {
            var gitResource = o.GetResource(context, "Inedo.Extensions.GitLab.Credentials.GitLabSecureResource", "GitLab");

            dynamic gitHubResource = gitResource;
            if (!string.IsNullOrEmpty(o.GroupName))
                gitHubResource.GroupName = o.GroupName;
            if (!string.IsNullOrEmpty(o.ProjectName))
                gitHubResource.ProjectName = o.ProjectName;
            if (!string.IsNullOrEmpty(o.ApiUrl))
                gitHubResource.ApiUrl = o.ApiUrl;

            return (((GitSecureCredentialsBase)gitResource.GetCredentials(context))?.ToUsernamePassword(), gitResource);
        }
    }
}
