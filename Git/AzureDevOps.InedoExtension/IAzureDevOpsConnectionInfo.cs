using System.Security;
using Inedo.Extensibility.Credentials;
using Inedo.Extensibility.Operations;
using Inedo.Extensibility.SecureResources;
using Inedo.Extensions.AzureDevOps.Credentials;

namespace Inedo.Extensions.AzureDevOps
{
    internal interface IAzureDevOpsConfiguration
    {
        string ResourceName { get; }
        string InstanceUrl { get; }
        string UserName { get; set; }
        SecureString Token { get; }
        string ProjectName { get; }
        string RepositoryName { get; }
    }
    internal static class AzureDevOpsOperationExtensions
    {
        public static string DescribeSource(this IOperationConfiguration config)
        {
            return AH.CoalesceString(
                config[nameof(IAzureDevOpsConfiguration.RepositoryName)],
                config[nameof(IAzureDevOpsConfiguration.ProjectName)],
                config[nameof(IAzureDevOpsConfiguration.ResourceName)],
                "(unknown)");
        }
        public static (AzureDevOpsSecureCredentials, AzureDevOpsSecureResource) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, IOperationExecutionContext context)
            => GetCredentialsAndResource(operation, (ICredentialResolutionContext)context);
        public static (AzureDevOpsSecureCredentials, AzureDevOpsSecureResource) GetCredentialsAndResource(this IAzureDevOpsConfiguration operation, ICredentialResolutionContext context)
        {
            AzureDevOpsSecureCredentials credentials; AzureDevOpsSecureResource resource;
            if (string.IsNullOrEmpty(operation.ResourceName))
            {
                credentials = operation.Token == null ? null : new AzureDevOpsSecureCredentials();
                resource = string.IsNullOrEmpty(operation.InstanceUrl) ? null : new AzureDevOpsSecureResource();
            }
            else
            {
                resource = (AzureDevOpsSecureResource)SecureResource.TryCreate(operation.ResourceName, context);
                if (resource == null)
                    credentials = null;
                else
                    credentials = (AzureDevOpsSecureCredentials)resource.GetCredentials(context);
            }

            if (credentials != null)
            {
                credentials.UserName = AH.CoalesceString(operation.UserName, credentials.UserName);
                credentials.Token = operation.Token ?? credentials.Token;
            }
            if (resource != null)
            {
                resource.InstanceUrl = AH.CoalesceString(operation.InstanceUrl, resource.InstanceUrl);
                resource.RepositoryName = AH.CoalesceString(operation.RepositoryName, resource.RepositoryName);
                resource.ProjectName = AH.CoalesceString(operation.ProjectName, resource.ProjectName);
            }

            return (credentials, resource);
        }
    }
}

