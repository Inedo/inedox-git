using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensions.Credentials;
using Inedo.Extensions.Credentials.Git;

namespace Inedo.Extensions.Git.Legacy
{
    internal interface ILegacyAzureDevOpsOperation : ILegacyGitOperation
    {
        string InstanceUrl { get; }
        string UserName { get; set; }
        SecureString Token { get; }
        string ProjectName { get; }
        string RepositoryName { get; }

        public static (UsernamePasswordCredentials, GitSecureResourceBase) GetCredentialsAndResource(ILegacyAzureDevOpsOperation o, ICredentialResolutionContext context)
        {
            var gitResource = o.GetResource(context, "Inedo.Extensions.AzureDevOps.Credentials.AzureDevOpsSecureResource", "AzureDevOps");

            dynamic azureDevOpsResource = gitResource;
            if (!string.IsNullOrEmpty(o.InstanceUrl))
                azureDevOpsResource.InstanceUrl = o.InstanceUrl;
            if (!string.IsNullOrEmpty(o.RepositoryName))
                azureDevOpsResource.RepositoryName = o.RepositoryName;
            if (!string.IsNullOrEmpty(o.ProjectName))
                azureDevOpsResource.ProjectName = o.ProjectName;

            return GetCredentials(gitResource, context, o.UserName, o.Token);
        }
    }
}
