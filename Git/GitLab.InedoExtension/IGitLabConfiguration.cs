using System;
using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;

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

    internal record GitLabProjectId(string GroupName, string ProjectName)
    {
        public GitLabProjectId(GitLabRepository repository)
            : this(repository.GroupName, repository.ProjectName)
        {
        }

        public static implicit operator GitLabProjectId(GitLabRepository r) => new(r);

        public string ToUriFragment()
        {
            if (!string.IsNullOrEmpty(this.GroupName))
                return Uri.EscapeDataString(this.GroupName + "/" + this.ProjectName);
            else
                return Uri.EscapeDataString(this.ProjectName ?? string.Empty);
        }
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

        public static (GitLabAccount, GitLabRepository) GetCredentialsAndResource(this IGitLabConfiguration operation, ICredentialResolutionContext context)
        {
            GitLabAccount credentials; GitLabRepository resource; 
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                credentials = string.IsNullOrEmpty(operation.UserName) ? null : new GitLabAccount();
                resource = string.IsNullOrEmpty(AH.CoalesceString(operation.ProjectName, operation.GroupName, operation.ApiUrl)) ? null : new GitLabRepository();
            }
            else
            {
                resource = (GitLabRepository)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                {
                    credentials = null;
                }
                else
                {
                    credentials = (GitLabAccount)resource.GetCredentials(context);
                }
            }

            if (credentials != null)
            {
                credentials.UserName = AH.CoalesceString(operation.UserName, credentials.UserName);
                credentials.PersonalAccessToken = operation.Password ?? credentials.PersonalAccessToken;
                credentials.ServiceUrl = operation.ApiUrl ?? credentials.ServiceUrl ?? resource?.LegacyApiUrl;
            }
            if (resource != null)
            {
                resource.GroupName = AH.CoalesceString(operation.GroupName, resource.GroupName);
                resource.ProjectName = AH.CoalesceString(operation.ProjectName, resource.ProjectName);
            }

            return (credentials, resource);
        }
    }
}
