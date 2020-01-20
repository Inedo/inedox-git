using System.Security;
using Inedo.Extensibility;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.GitLab.Credentials;

namespace Inedo.Extensions.GitLab
{
    internal interface IGitLabConfiguration
    {
        string ResourceName { get;  }
        string GroupName { get;  }
        string ProjectName { get; }
        string ApiUrl { get;  }
        SecureString Password { get; }
        string UserName { get; }
    }
    internal sealed class GitLabConfiguration : IGitLabConfiguration
    {
        public string ResourceName { get; set; }
        public string GroupName { get; set; }
        public string ProjectName { get; set; }
        public string ApiUrl { get; set; }
        public SecureString Password { get; set; }
        public string UserName { get; set; }
    }
    internal static class GitLabOperationExtensions 
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IGitLabConfiguration.ProjectName)],
                config[nameof(IGitLabConfiguration.ResourceName)],
                "(unknown)");
        }
        public static (GitLabSecureCredentials, GitLabSecureResource) GetCredentialsAndResource(this IComponentConfiguration config)
        {
            return GetCredentialsAndResource(
                new GitLabConfiguration
                {
                    ApiUrl = AH.NullIf(config[nameof(IGitLabConfiguration.ApiUrl)], string.Empty),
                    GroupName = AH.NullIf(config[nameof(IGitLabConfiguration.GroupName)], string.Empty),
                    Password = string.IsNullOrEmpty(config[nameof(IGitLabConfiguration.Password)]) ? null : AH.CreateSecureString(config[nameof(IGitLabConfiguration.Password)]),
                    ProjectName = AH.NullIf(config[nameof(IGitLabConfiguration.ProjectName)], string.Empty),
                    ResourceName = AH.NullIf(config[nameof(IGitLabConfiguration.ResourceName)], string.Empty),
                    UserName = AH.NullIf(config[nameof(IGitLabConfiguration.UserName)], string.Empty)
                },
                new CredentialResolutionContext((config.EditorContext as ICredentialResolutionContext)?.ApplicationId, null));
        }
        public static (GitLabSecureCredentials, GitLabSecureResource) GetCredentialsAndResource(this IGitLabConfiguration operation, ICredentialResolutionContext context)
        {
            GitLabSecureCredentials credentials = null;
            GitLabSecureResource resource = null;
            if (!string.IsNullOrEmpty(operation.ResourceName))
            {
                resource = (GitLabSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    var rc = SecureCredentials.TryCreate(operation.ResourceName, context) as GitLabLegacyResourceCredentials;
                    resource = (GitLabSecureResource)rc?.ToSecureResource();
                    credentials = (GitLabSecureCredentials)rc?.ToSecureCredentials();
                }
                else
                {
                    credentials = (GitLabSecureCredentials)resource.GetCredentials(context);
                }
            }

            return (
                string.IsNullOrEmpty(AH.CoalesceString(operation.UserName, credentials?.UserName)) ? null : new GitLabSecureCredentials
                {
                    UserName = AH.CoalesceString(operation.UserName, credentials?.UserName),
                    PersonalAccessToken = operation.Password ?? credentials?.PersonalAccessToken
                },
                new GitLabSecureResource
                {
                    ApiUrl = AH.CoalesceString(operation.ApiUrl, resource?.ApiUrl),
                    GroupName = AH.CoalesceString(operation.GroupName, resource?.GroupName),
                    ProjectName = AH.CoalesceString(operation.ProjectName, resource?.ProjectName)
                }
            );
        }
    }
}
